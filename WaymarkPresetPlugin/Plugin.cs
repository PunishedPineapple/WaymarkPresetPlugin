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
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using Newtonsoft.Json.Schema;

namespace WaymarkPresetPlugin
{
	public class Plugin : IDalamudPlugin
	{
		//	Initialization
		public void Initialize( DalamudPluginInterface pluginInterface )
		{
			//	Configuration
			mPluginInterface = pluginInterface;
			mConfiguration = mPluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
			mConfiguration.Initialize( mPluginInterface );
			mGameMemoryHandler = new MemoryHandler( mPluginInterface );

			//	Get the game sheets that we need to populate a zone dictionary.
			ExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType> territorySheet = mPluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>();
			ExcelSheet<Lumina.Excel.GeneratedSheets.PlaceName> placeNameSheet = mPluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.PlaceName>();
			ExcelSheet<Lumina.Excel.GeneratedSheets.ContentFinderCondition> contentFinderSheet = mPluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ContentFinderCondition>();
			
			//	Get the name for every "MapID" that is an instance zone.
			Dictionary<UInt16, Tuple<string, string>> zoneNames = new Dictionary<UInt16, Tuple<string, string>>();
			foreach( TerritoryType zone in territorySheet.GetRows() )
			{
				//*****TODO: unknown24 should be changed to unknown10 for new version of Dalamud.  Currently unknown10 in test version.*****
				if( zone.ExclusiveType == 2 && !zoneNames.ContainsKey( zone.unknown24 ) )
				{
					string dutyName = contentFinderSheet.GetRow( zone.unknown24 ).Name.Trim();
					if( dutyName.Length > 0 )
					{
						dutyName = dutyName.First().ToString().ToUpper() + dutyName.Substring( 1 );
					}
					zoneNames.Add( zone.unknown24, Tuple.Create( dutyName, placeNameSheet.GetRow( zone.PlaceName ).Name ) );
				}
			}
			//	There are several zones with an "ID" of zero, but none of them allow waymarks to be saved, so change the name of that zone to reflect this.
			if( zoneNames.ContainsKey( 0 ) )
			{
				zoneNames[0] = Tuple.Create( "Unknown Zone", "Unknown Zone" );
			}

			//	Text Command Initialization
			mPluginInterface.CommandManager.AddHandler( mTextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = "Performs waymark preset commands.  Use \"/pwaymark help\" for detailed usage information."
			} );

			//	UI Initialization
			mUI = new PluginUI( this.mConfiguration, zoneNames, mGameMemoryHandler );
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
				mUI.MainWindowVisible = true;
			}
			else if( subCommand.ToLower() == "config" )
			{
				mUI.SettingsWindowVisible = true;
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
			if( args.ToLower() == "commands" )
			{
				return "Valid commands are as follows: import, export, slotinfo, writeslot, libraryinfo, config.  If no command is provided, the GUI will be opened.  Type /pwaymark help <command> for usage information.";
			}
			else if( args.ToLower() == "config" )
			{
				return "Opens the settings window.";
			}
			else if( args.ToLower() == "import" )
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
				return "Use \"/pwaymark\" to open the GUI.  Use \"/pwaymark commands\" for a list of text commands.";
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
					try
					{
						WaymarkPreset tempPreset = WaymarkPreset.Parse( mGameMemoryHandler.ReadSlot( gameSlotToCopy ) );
						tempPreset.Name = "Imported";//*****TODO*****
						int importedIndex = mConfiguration.PresetLibrary.ImportPreset( tempPreset );

						if( importedIndex >= 0 )
						{
							mConfiguration.Save();
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
						mConfiguration.Save();
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
			int slotNum;
			if( args.Length > 0 )
			{
				if( args[0] == 'g' )
				{
					args = args.Substring( 1 );
					if( int.TryParse( args, out slotNum ) && slotNum >= 1 && slotNum <= 5 )
					{
						byte[] gamePreset = new byte[104];
						IntPtr pGameData = mGameMemoryHandler.GetGameWaymarkDataPointer();
						try
						{
							mGameMemoryHandler.ReadSlot( slotNum );
							WaymarkPreset tempPreset = WaymarkPreset.Parse( gamePreset );
							tempPreset.Name = "Exported preset";//*****TODO*****
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
						return "An invalid slot number was specified.";
					}
				}
				else if( int.TryParse( args, out slotNum ) && slotNum >= 0 && slotNum < mConfiguration.PresetLibrary.Presets.Count )
				{
					return mConfiguration.PresetLibrary.ExportPreset( slotNum );
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

				try
				{
					WaymarkPreset tempPreset = WaymarkPreset.Parse( mGameMemoryHandler.ReadSlot( gameSlotToCopy ) );
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
							//Copy from library
							if( CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[sourceLibraryIndex], targetGameSlot ) )
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
								if( CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[sourceLibraryIndex], targetGameSlot ) )
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
			if( slot >= 1 && slot <= 5 )
			{
				byte[] gamePresetData = preset.ConstructGamePreset();
				if( gamePresetData.Length == 104 )
				{
					try
					{
						return mGameMemoryHandler.WriteSlot( slot, gamePresetData );
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

		protected void DrawUI()
		{
			mUI.Draw();
		}

		protected void DrawConfigUI()
		{
			mUI.SettingsWindowVisible = true;
		}

		public string Name => "WaymarkPresetPlugin";
		protected const string mTextCommandName = "/pwaymark";

		protected DalamudPluginInterface mPluginInterface;
		protected Configuration mConfiguration;
		protected PluginUI mUI;
		protected MemoryHandler mGameMemoryHandler;
	}
}
