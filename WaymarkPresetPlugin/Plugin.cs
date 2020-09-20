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
			MemoryHandler.Init( mPluginInterface );
			ZoneInfoHandler.Init( mPluginInterface );

			//	Text Command Initialization
			mPluginInterface.CommandManager.AddHandler( mTextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = "Performs waymark preset commands.  Use \"/pwaymark help\" for detailed usage information."
			} );

			//	UI Initialization
			mUI = new PluginUI( mConfiguration, mPluginInterface );
			mPluginInterface.UiBuilder.OnBuildUi += DrawUI;
			mPluginInterface.UiBuilder.OnOpenConfigUi += ( sender, args ) => DrawConfigUI();
			mUI.SetCurrentTerritoryTypeID( mPluginInterface.ClientState.TerritoryType );

			//	Event Subscription
			mPluginInterface.ClientState.TerritoryChanged += OnTerritoryChanged;

			//	Tell the user if there's something out of the ordinary.
			if( !MemoryHandler.FoundSavedPresetSigs() )
			{
				mPluginInterface.Framework.Gui.Chat.Print( "Error initializing WaymarkPresetPlugin: Cannot write to or read from game." );
			}
		}

		//	Cleanup
		public void Dispose()
		{
			MemoryHandler.Uninit();
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
				mUI.MainWindowVisible = !mUI.MainWindowVisible;
			}
			else if( subCommand.ToLower() == "config" )
			{
				mUI.SettingsWindowVisible = !mUI.SettingsWindowVisible;
			}
			else if( subCommand.ToLower() == "slotinfo" )
			{
				commandResponse = ProcessTextCommand_SlotInfo( subCommandArgs );
			}
			else if( subCommand.ToLower() == "place" )
			{
				commandResponse = ProcessTextCommand_Place( subCommandArgs );
			}
			else if( subCommand.ToLower() == "exportall" )
			{
				commandResponse = ProcessTextCommand_ExportAll( subCommandArgs );
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

		protected string ProcessTextCommand_Help( string args )
		{
			if( args.ToLower() == "commands" )
			{
				return $"Valid commands are as follows: config, slotinfo, exportall{( mConfiguration.AllowDirectPlacePreset ? ", place" : "" )}.  If no command is provided, the GUI will be opened.  Type /pwaymark help <command> for usage information.";
			}
			else if( args.ToLower() == "config" )
			{
				return "Opens the settings window.";
			}
			else if( args.ToLower() == "slotinfo" )
			{
				return "Prints the data saved in the game's slots to the chat window.  Usage \"/pwaymark slotinfo <slot>\".  The slot number can be any valid game slot.";
			}
			else if( mConfiguration.AllowDirectPlacePreset && args.ToLower() == "place" )
			{
				return "Places the preset at the specified library index (if possible).  Usage \"/pwaymark place <index>\".  Index can be any valid libary preset number.";
			}
			else if( args.ToLower() == "exportall" )
			{
				return "Copies all presets in the library to the clipboard, one per line.  Add -t if you wish to include the last-modified timestamp in the export.";
			}
			else
			{
				return "Use \"/pwaymark\" to open the GUI.  Use \"/pwaymark help commands\" for a list of text commands.";
			}
		}

		protected string ProcessTextCommand_SlotInfo( string args )
		{
			uint gameSlotToCopy;
			if( args.Length == 1 &&
				uint.TryParse( args, out gameSlotToCopy ) &&
				gameSlotToCopy >= 1 &&
				gameSlotToCopy <= 5 )
			{
				if( MemoryHandler.FoundSavedPresetSigs() )
				{
					byte[] gamePreset = new byte[104];

					try
					{
						WaymarkPreset tempPreset = WaymarkPreset.Parse( MemoryHandler.ReadSlot( gameSlotToCopy ) );
						return "Slot " + gameSlotToCopy.ToString() + " Contents:\r\n" + tempPreset.GetPresetDataString( mConfiguration.GetZoneNameDelegate, mConfiguration.ShowIDNumberNextToZoneNames );
					}
					catch( Exception e )
					{
						PluginLog.Log( $"An unknown error occured while trying to read the game's waymark data: {e}" );
						return "An unknown error occured while trying to read the game's waymark data.";
					}
				}
				else
				{
					return "Unable to read game's waymark data.";
				}
			}
			else
			{
				return "An invalid game slot number was provided.";
			}
		}

		protected string ProcessTextCommand_Place( string args )
		{
			if( mConfiguration.AllowDirectPlacePreset )
			{
				int slotNum = -1;
				bool validSlot = int.TryParse( args.Trim(), out slotNum ) && slotNum >= 0 && slotNum < mConfiguration.PresetLibrary.Presets.Count;
				if( validSlot )
				{
					try
					{
						MemoryHandler.DirectPlacePreset( mConfiguration.PresetLibrary.Presets[slotNum].ConstructGamePreset() );
						return "";
					}
					catch( Exception e )
					{
						PluginLog.Log( $"An unknown error occured while attempting to place preset {slotNum} : {e}" );
						return $"An unknown error occured placing preset {slotNum}.";
					}
				}
				else
				{
					return $"Invalid preset number \"{slotNum}\"";
				}
			}
			else
			{
				return "Direct placement from the library is not currently allowed; see the plugin settings for more information.";
			}
		}

		protected string ProcessTextCommand_ExportAll( string args )
		{
			try
			{
				string str = "";
				if( args.ToLower().Trim() == "-t" )
				{
					foreach( var preset in mConfiguration.PresetLibrary.Presets )
					{
						str += WaymarkPresetExport.GetExportString( preset ) + "\r\n";
					}
				}
				else
				{
					foreach( var preset in mConfiguration.PresetLibrary.Presets )
					{
						str += JsonConvert.SerializeObject( preset ) + "\r\n";
					}
				}

				Win32Clipboard.CopyTextToClipboard( str );

				return "";
			}
			catch( Exception e )
			{
				PluginLog.Log( $"Unknown error occured while trying to copy presets to clipboard: {e}" );
				return "Unknown error occured while trying to copy presets to clipboard.";
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

		protected void OnTerritoryChanged( object sender, UInt16 ID )
		{
			ZoneInfo prevTerritoryTypeInfo = ZoneInfoHandler.GetZoneInfoFromTerritoryTypeID( CurrentTerritoryTypeID );
			ZoneInfo newTerritoryTypeInfo = ZoneInfoHandler.GetZoneInfoFromTerritoryTypeID( ID );
			CurrentTerritoryTypeID = ID;
			mUI.SetCurrentTerritoryTypeID( ID );

			//	Auto-save presets on leaving instance.
			if( mConfiguration.AutoSavePresetsOnInstanceLeave && ZoneInfoHandler.IsKnownContentFinderID( prevTerritoryTypeInfo.ContentFinderConditionID ) )
			{
				for( uint i = 1; i <= 5; ++i )
				{
					try
					{
						var preset = WaymarkPreset.Parse( MemoryHandler.ReadSlot( i ) );
						if( preset.MapID == prevTerritoryTypeInfo.ContentFinderConditionID && !mConfiguration.PresetLibrary.Presets.Any( x => x.Equals( preset ) ) )
						{
							preset.Name = prevTerritoryTypeInfo.DutyName.ToString() + " - AutoImported";
							mConfiguration.PresetLibrary.Presets.Add( preset );
						}
					}
					catch( Exception e )
					{
						PluginLog.Log( $"Error while attempting to auto-import game slot {i}: {e}" );
					}
				}

				mConfiguration.Save();
			}

			//	Auto-load presets on entering instance.
			if( mConfiguration.AutoPopulatePresetsOnEnterInstance && ZoneInfoHandler.IsKnownContentFinderID( newTerritoryTypeInfo.ContentFinderConditionID ) )
			{
				//*****TODO: Eventually maybe have this check for a "preferred" flag on the presets and use that to help select which five to use, rather than just the first five from the zone.
				var presetsToAutoLoad = mConfiguration.PresetLibrary.Presets.Where( x => x.MapID == newTerritoryTypeInfo.ContentFinderConditionID ).Take( 5 ).ToList();
				for( int i = 0; i < 5; ++i )
				{
					//	Start out with an array of zeroes (which is how the game indicates an empty slot), and get an actual preset if we can.
					byte[] gamePresetData = new byte[104];

					if( i < presetsToAutoLoad.Count )
					{
						var preset = presetsToAutoLoad[i];
						gamePresetData = preset.ConstructGamePreset();
					}

					if( gamePresetData.Length == 104 )
					{
						try
						{
							MemoryHandler.WriteSlot( (uint)i + 1, gamePresetData );
						}
						catch( Exception e )
						{
							PluginLog.Log( $"Error while auto copying preset data to game slot {i}: {e}" );
						}
					}
				}
			}
		}

		public string Name => "WaymarkPresetPlugin";
		protected const string mTextCommandName = "/pwaymark";

		public UInt16 CurrentTerritoryTypeID { get; protected set; }

		protected DalamudPluginInterface mPluginInterface;
		protected Configuration mConfiguration;
		protected PluginUI mUI;
	}
}
