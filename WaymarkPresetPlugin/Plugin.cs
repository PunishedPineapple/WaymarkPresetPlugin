using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace WaymarkPresetPlugin
{
	public class Plugin : IDalamudPlugin
	{
		//	Types
		public delegate IntPtr GetConfigFileDelegate( byte fileIndex );
		public delegate IntPtr GetConfigSectionDelegate( IntPtr pConfigFile, byte sectionIndex );

		//	Initialization
		public void Initialize( DalamudPluginInterface pluginInterface )
		{
			//	Configuration
			mPluginInterface = pluginInterface;
			mConfiguration = mPluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
			mConfiguration.Initialize( this.mPluginInterface );

			//	Get Function Pointers
			var getConfigFileAddress = mPluginInterface.TargetModuleScanner.ScanText( "E8 ?? ?? ?? ?? 48 85 C0 74 14 83 7B 44 00" );
			if( getConfigFileAddress != IntPtr.Zero )
			{
				mfpGetConfigFile = Marshal.GetDelegateForFunctionPointer<GetConfigFileDelegate>( getConfigFileAddress );
			}
			var getConfigSectionAddress = mPluginInterface.TargetModuleScanner.ScanText( "40 53 48 83 EC 20 48 8B 0D ?? ?? ?? ?? 0F B7 DA" );
			if( getConfigSectionAddress != IntPtr.Zero )
			{
				mfpGetConfigSection = Marshal.GetDelegateForFunctionPointer<GetConfigSectionDelegate>( getConfigSectionAddress );
			}

			//	Text Command Initialization
			mPluginInterface.CommandManager.AddHandler( mTextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = "Performs waymark preset commands.  Use \"/pwaymark help\" for detailed usage information."
			} );

			//	UI Initialization
			mUI = new PluginUI( this.mConfiguration );
			mPluginInterface.UiBuilder.OnBuildUi += DrawUI;
			mPluginInterface.UiBuilder.OnOpenConfigUi += ( sender, args ) => DrawConfigUI();
		}

		//	Cleanup
		public void Dispose()
		{
			mUI.Dispose();
			mPluginInterface.CommandManager.RemoveHandler( mTextCommandName );
			mPluginInterface.Dispose();
		}

		//	Text Commands
		protected void ProcessTextCommand( string command, string args )
		{
			//*****TODO: Don't split, just substring off of the first space so that other stuff is preserved verbatim.
			//	Seperate into sub-command and paramters.
			string subCommand = "";
			string subCommandArgs = "";
			string[] argsArray = args.Split( ' ' );
			if( argsArray.Length > 0 )
			{
				subCommand = argsArray[0];
			}
			if( argsArray.Length > 1 )
			{
				//	Recombine because there might be spaces in JSON or something, that would make splitting it bad.
				for( int i = 1; i < argsArray.Length; ++i )
				{
					subCommandArgs += argsArray[i] + ' ';
				}
				subCommandArgs = subCommandArgs.Trim();
			}

			//	Process the commands.
			string commandResponse = "";
			if( subCommand.Length == 0 )
			{
				//*****TODO: Open the GUI if nothing is provided.*****
				mUI.MainWindowVisible = true;
			}
			else if( subCommand.ToLower() == "import" )
			{
				commandResponse = ProcessTextCommand_Import( subCommandArgs );
			}
			else if( subCommand.ToLower() == "export" )
			{
				commandResponse = ProcessTextCommand_Export( subCommandArgs );
			}
			else if( subCommand.ToLower() == "slotinfo" )
			{
				commandResponse = ProcessTextCommand_SlotInfo( subCommandArgs );
			}
			else if( args.Trim().StartsWith( "writeslot" ) )
			{
				commandResponse = ProcessTextCommand_WriteSlot( subCommandArgs );
			}
			else if( subCommand.ToLower() == "libraryinfo" )
			{
				commandResponse = ProcessTextCommand_LibraryInfo( subCommandArgs );
			}
			else if( args.Trim().StartsWith( "save" ) )	//*****TODO: Remove Dev Command.  Figure out when saving should be done and do it automatically.
			{
				mConfiguration.Save();
				commandResponse = "Saved Config";
			}
			else
			{
				commandResponse = ProcessTextCommand_Help( subCommandArgs );
			}

			//	Send any feedback to the user.
			if( commandResponse.Length > 0 )
			{
				mPluginInterface.Framework.Gui.Chat.Print( commandResponse );
			}
		}

		public string ProcessTextCommand_Help( string args )
		{
			if( args.ToLower() == "import" )
			{
				return "Imports a preset into the library.  Usage \"/pwaymark import <dataToImport>\".  If dataToImport is a single number, it copies that slot from the game's presets.  If a PP-formatted JSON string is provided instead, it will attempt to create a preset from that data.";
			}
			else if( args.ToLower() == "export" )
			{
				return "Exports a preset from the library into the chat window.  Usage \"/pwaymark export (g)<index>\".  If a 'g' precedes the index, it will export the specified game preset slot; otherwise it will export the preset at the specified library index.  Export format is PP-format JSON.";
			}
			else if( args.ToLower() == "slotinfo" )
			{
				return "Prints the data saved in the game's slots to the chat window.  Usage \"/pwaymark slotinfo <slot>\".  The slot number can be any valid game slot.";
			}
			else if( args.ToLower() == "writeslot" )
			{
				return "Writes the specified data to the specified preset slot.  Usage \"/pwaymark writeslot <slot> <index>|<json>\".  The slot number can be any valid game slot.  The second parameter can be either a valid library index, or valid PP-format JSON.  Please note that many PP exports have invalid MapIDs, so using this command with JSON data is generally not recommended.";
			}
			else if( args.ToLower() == "libraryinfo" )
			{
				return "Prints basic info about each preset in the library to the chat window.  Usage \"/pwaymark libraryinfo\".";
			}
			else
			{
				return "Valid commands are as follows: import, export, slotinfo, writeslot, libraryinfo.  If no command is provided, the GUI will be opened.  Type /pwaymark help <command> for usage information.";
			}
		}

		public string ProcessTextCommand_Import( string args )
		{
			if( args.Length < 1 )
			{
				return "No parameters were specified for the import command.  Use \"/pwaymark help import\" for proper command format.";
			}
			else if( args.Length == 1 )
			{
				int gameSlotToCopy;
				if( int.TryParse( args, out gameSlotToCopy ) && gameSlotToCopy >= 1 && gameSlotToCopy <= 5 )
				{
					byte[] gamePreset = new byte[104];
					IntPtr pGameData = GetGameWaymarkDataPointer();
					if( pGameData != IntPtr.Zero )
					{
						try
						{
							Marshal.Copy( new IntPtr( pGameData.ToInt64() + ( gameSlotToCopy - 1 ) * 104 ), gamePreset, 0, 104 );
							WaymarkPreset tempPreset = WaymarkPreset.Parse( gamePreset );
							int importedIndex = mConfiguration.PresetLibrary.ImportPreset( tempPreset );

							if( importedIndex >= 0 )
							{
								return "Slot " + gameSlotToCopy.ToString() + " added to library as index " + importedIndex + ".";
							}
							else
							{
								return "An unknown error occured while trying to import the game's waymark data.";
							}
						}
						catch( Exception e )
						{
							//*****TODO: Log exception somewhere.
							return "An unknown error occured while trying to read the game's waymark data.";
						}
					}
					else
					{
						return "Unable to locate game's waymark data!";
					}
				}
				else
				{
					return "Invalid waymark slot number passed to import command.  Only the numbers 1-5 are valid slots.";
				}
			}
			else
			{
				try
				{
					int importedIndex = mConfiguration.PresetLibrary.ImportPreset( args );
					if( importedIndex >= 0 )
					{
						return "Waymark preset imported as libary index " + importedIndex.ToString() + ".";
					}
					else
					{
						return "Unknown error while importing preset.";
					}
				}
				catch( Exception e )
				{
					//*****TODO: Log exception somewhere.*****
					return "Invalid JSON passed to import command, unable to import.";
				}
			}
		}

		public string ProcessTextCommand_Export( string args )
		{
			int slotIndex;
			if( args.Length > 0 )
			{
				if( args[0] == 'g' )
				{
					args = args.Substring( 1 );
					if( int.TryParse( args, out slotIndex ) && slotIndex >= 1 && slotIndex <= 5 )
					{
						byte[] gamePreset = new byte[104];
						IntPtr pGameData = GetGameWaymarkDataPointer();
						if( pGameData != IntPtr.Zero )
						{
							try
							{
								Marshal.Copy( new IntPtr( pGameData.ToInt64() + ( slotIndex - 1 ) * 104 ), gamePreset, 0, 104 );
								WaymarkPreset tempPreset = WaymarkPreset.Parse( gamePreset );
								//*****TODO: Set the preset name to something useful before serializing.*****
								return JsonConvert.SerializeObject( tempPreset );
							}
							catch( Exception e )
							{
								//*****TODO: Log exception somewhere.*****
								return "An unknown error occured while trying to read the game's waymark data.";
							}
						}
						else
						{
							return "Unable to locate game's waymark data!";
						}
					}
					else
					{
						return "An invalid slot number was specified.";
					}
				}
				else if( int.TryParse( args, out slotIndex ) && slotIndex >= 0 && slotIndex < mConfiguration.PresetLibrary.Presets.Count )
				{
					return mConfiguration.PresetLibrary.ExportPreset( slotIndex );
				}
				else
				{
					return "Invalid parameters were specified for the export command.  Use \"/pwaymark help export\" for proper command format.";
				}
			}
			else
			{
				return "No parameters were specified for the export command.  Use \"/pwaymark help export\" for proper command format.";
			}
		}

		public string ProcessTextCommand_SlotInfo( string args )
		{
			int gameSlotToCopy;
			if( args.Length == 1 &&
				int.TryParse( args, out gameSlotToCopy ) &&
				gameSlotToCopy >= 1 &&
				gameSlotToCopy <= 5 )
			{
				byte[] gamePreset = new byte[104];
				IntPtr pGameData = GetGameWaymarkDataPointer();
				if( pGameData != IntPtr.Zero )
				{
					try
					{
						Marshal.Copy( new IntPtr( pGameData.ToInt64() + ( gameSlotToCopy - 1 ) * 104 ), gamePreset, 0, 104 );
						WaymarkPreset tempPreset = WaymarkPreset.Parse( gamePreset );

						return "Slot " + gameSlotToCopy.ToString() + " Contents:\r\n" + tempPreset.GetPresetDataString();
					}
					catch( Exception e )
					{
						//*****TODO: Log exception somewhere.
						return "An unknown error occured while trying to read the game's waymark data.";
					}
				}
				else
				{
					return "Unable to locate game's waymark data!";
				}
			}
			else
			{
				return "An invalid game slot number was provided.";
			}
		}
		
		public string ProcessTextCommand_WriteSlot( string args )
		{
			if( args.Trim().Split( ' ' ).Length > 0 )
			{
				string firstArg = args.Trim().Split( ' ' )[0];
				string nextArgs = args.Substring( args.IndexOf( firstArg ) + firstArg.Length ).Trim( ' ' );

				if( nextArgs.Length > 0 )
				{
					int targetGameSlot;
					int sourceLibraryIndex;
					if( int.TryParse( firstArg, out targetGameSlot ) && targetGameSlot >= 1 && targetGameSlot <= 5 )
					{
						if( int.TryParse( nextArgs, out sourceLibraryIndex ) && sourceLibraryIndex >=0 && sourceLibraryIndex < mConfiguration.PresetLibrary.Presets.Count )
						{
							/*IntPtr pWaymarkMemory = GetGameWaymarkDataPointer();
							if( pWaymarkMemory != IntPtr.Zero )
							{
								if( targetGameSlot >= 1 && targetGameSlot <= 5 )
								{

								}
							}
							return	"Want to write data to slot " + targetGameSlot.ToString() +
									" at address 0x" + new IntPtr( pWaymarkMemory.ToInt64() + ( ( targetGameSlot - 1 ) * 104 ) ).ToString( "X" ) +
									" (raw address 0x " + pWaymarkMemory.ToString( "X" ) + ")";*/
							
							//Copy from library
							if( CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[sourceLibraryIndex], targetGameSlot - 1 ) )
							{
								return "Library index " + sourceLibraryIndex.ToString() + " successfully copied to game slot " + targetGameSlot.ToString() + ".";
							}
							else
							{
								return "Failed to copy library index " + sourceLibraryIndex.ToString() + " to game slot " + targetGameSlot.ToString() + "!";
							}
						}
						else
						{
							//deserialize json into temporary waymark, and import that.
							try
							{
								WaymarkPreset tempPreset = JsonConvert.DeserializeObject<WaymarkPreset>( nextArgs );
								if( CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[sourceLibraryIndex], targetGameSlot - 1 ) )
								{
									return "Successfully imported the provided preset data to slot " + targetGameSlot.ToString() + ".";
								}
								else
								{
									return "Failed to import the provided preset to data slot " + targetGameSlot.ToString() + "!";
								}
							}
							catch( Exception e )
							{
								//*****TODO: Log the error.*****
								return "An unexpected error occured while trying to import the provided preset to data slot " + targetGameSlot.ToString() + "!";
							}
						}
					}
					else
					{
						return "Invalid game slot number specified.";
					}
				}
				else
				{
					return "No source preset specified.  Use \"/pwaymark help writeslot\" for proper command format.";
				}
			}
			else
			{
				return "No arguments provided.  Use \"/pwaymark help writeslot\" for proper command format.";
			}
		}
		
		public string ProcessTextCommand_LibraryInfo( string args )
		{
			//*****TODO: Print more detailed information about a single preset if an index is provided.*****
			string str = "Waymark Libary Contents:";
			int counter = 0;
			foreach( WaymarkPreset preset in mConfiguration.PresetLibrary.Presets )
			{
				str += "\r\n" + counter.ToString().PadLeft( 3 ) + ": " + preset.Name;
				++counter;
			}
			return str;
		}

		protected bool CopyPresetToGameSlot( WaymarkPreset preset, int slot )
		{
			IntPtr pWaymarkMemory = GetGameWaymarkDataPointer();
			if( pWaymarkMemory != IntPtr.Zero )
			{
				if( slot >= 0 && slot < 5 )
				{
					byte[] gamePresetData = preset.ConstructGamePreset();
					if( gamePresetData.Length == 104 )
					{
						try
						{
							Marshal.Copy( gamePresetData, 0, new IntPtr( pWaymarkMemory.ToInt64() + ( slot * 104 ) ), 104 );
							return true;
						}
						catch( Exception e )
						{
							//*****TODO: Log problem.*****
							return false;
						}
					}
					else
					{
						//*****TODO: Log problem.*****
						return false;
					}
				}
				else
				{
					//*****TODO: Log problem.*****
					return false;
				}
			}
			else
			{
				//*****TODO: Log error.*****
				return false;
			}
		}

		protected void DrawUI()
		{
			mUI.Draw();
		}

		protected void DrawConfigUI()
		{
			mUI.SettingsWindowVisible = true;
		}

		protected IntPtr GetGameWaymarkDataPointer()
		{
			//*****TODO: Check that function pointers were assigned properly.*****
			IntPtr pWaymarksLocation = mfpGetConfigSection.Invoke( mfpGetConfigFile.Invoke( 0x9 ), 0x11 );

			//*****TODO: We don't know if it will always be 48 bytes to the start of FMARKER.DAT.  Maybe searching forward a bit in memory from the address we get to find that string is safer.*****
			//	64 additional bytes from the pointer given to us until the actual preset data.
			if( pWaymarksLocation != IntPtr.Zero )
			{
				pWaymarksLocation = new IntPtr( pWaymarksLocation.ToInt64() + 64L );
			}

			return pWaymarksLocation;
		}

		protected static GetConfigFileDelegate mfpGetConfigFile;
		protected static GetConfigSectionDelegate mfpGetConfigSection;

		public string Name => "WaymarkPresetPlugin";
		protected const string mTextCommandName = "/pwaymark";

		protected DalamudPluginInterface mPluginInterface;
		protected Configuration mConfiguration;
		protected PluginUI mUI;
	}
}
