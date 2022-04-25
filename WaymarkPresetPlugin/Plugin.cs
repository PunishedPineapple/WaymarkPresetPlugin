using Dalamud.Plugin;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game;
using Dalamud.Data;
using Dalamud.Logging;
using System;
using System.Linq;
using Newtonsoft.Json;

namespace WaymarkPresetPlugin
{
	public class Plugin : IDalamudPlugin
	{
		//	Initialization
		public Plugin(
			DalamudPluginInterface pluginInterface,
			ClientState clientState,
			CommandManager commandManager,
			Condition condition,
			ChatGui chatGui,
			GameGui gameGui,
			DataManager dataManager,
			SigScanner sigScanner )
		{
			//	API Access
			mPluginInterface	= pluginInterface;
			mClientState		= clientState;
			mCommandManager		= commandManager;
			mCondition			= condition;
			mChatGui			= chatGui;
			mGameGui			= gameGui;
			mSigScanner			= sigScanner;
			mDataManager		= dataManager;

			//	Configuration
			mConfiguration = mPluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
			mConfiguration.Initialize( mPluginInterface );
			MemoryHandler.Init( mSigScanner, mClientState, mCondition );
			ZoneInfoHandler.Init( mDataManager );

			//	Text Command Initialization
			mCommandManager.AddHandler( mTextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = "Performs waymark preset commands.  Use \"/pwaymark help\" for detailed usage information."
			} );

			//	UI Initialization
			mUI = new PluginUI( mConfiguration, mPluginInterface, mDataManager, mCommandManager, mGameGui );
			mPluginInterface.UiBuilder.Draw += DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
			mUI.SetCurrentTerritoryTypeID( mClientState.TerritoryType );
			mUI.Initialize();

			//	Event Subscription
			mClientState.TerritoryChanged += OnTerritoryChanged;

			//	Tell the user if there's something out of the ordinary.
			if( !MemoryHandler.FoundSavedPresetSigs() )
			{
				mChatGui.Print( "Error initializing WaymarkPresetPlugin: Cannot write to or read from game." );
			}
		}

		//	Cleanup
		public void Dispose()
		{
			MemoryHandler.Uninit();
			mUI.Dispose();
			mClientState.TerritoryChanged -= OnTerritoryChanged;
			mPluginInterface.UiBuilder.Draw -= DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
			mCommandManager.RemoveHandler( mTextCommandName );
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
			bool suppressResponse = mConfiguration.SuppressCommandLineResponses;
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
			else if( subCommand.ToLower() == "import" )
			{
				commandResponse = ProcessTextCommand_Import( subCommandArgs );
			}
			else if( subCommand.ToLower() == "export" )
			{
				commandResponse = ProcessTextCommand_Export( subCommandArgs );
			}
			else if( subCommand.ToLower() == "exportall" )
			{
				commandResponse = ProcessTextCommand_ExportAll( subCommandArgs );
			}
			else if( subCommand.ToLower() == "help" || subCommand.ToLower() == "?" )
			{
				commandResponse = ProcessTextCommand_Help( subCommandArgs );
				suppressResponse = false;
			}
			else
			{
				commandResponse = ProcessTextCommand_Help( subCommandArgs );
			}

			//	Send any feedback to the user.
			if( commandResponse.Length > 0 && !suppressResponse )
			{
				mChatGui.Print( commandResponse );
			}
		}

		protected string ProcessTextCommand_Help( string args )
		{
			if( args.ToLower() == "commands" )
			{
				return $"Valid commands are as follows: config, slotinfo, import, export, exportall, and place.  If no command is provided, the GUI will be opened.  Type /pwaymark help <command> for usage information.";
			}
			else if( args.ToLower() == "config" )
			{
				return "Opens the settings window.";
			}
			else if( args.ToLower() == "slotinfo" )
			{
				return "Prints the data saved in the game's slots to the chat window.  Usage \"/pwaymark slotinfo <slot>\".  The slot number can be any valid game slot.";
			}
			else if( args.ToLower() == "place" )
			{
				return "Places the preset with the specified name (if possible).  Quotes MUST be used around the name.  May also specify preset index without quotes instead.  Usage \"/pwaymark place \"<name>\"|<index>\".  Name must match exactly (besides case).  Index can be any valid libary preset number.";
			}
			else if( args.ToLower() == "import" )
			{
				return "Copies one of the game's five preset slots to the library.  Usage \"/pwaymark import <slot>\".  The slot number can be any valid game slot.  Command-line import of a formatted preset string is not supported due to length restrictions in the game's chat box.";
			}
			else if( args.ToLower() == "export" )
			{
				return "Copies a preset from the library to the specified game slot *or* copies a preset to the clipboard, depending on flags and parameters.  Usage \"/pwaymark export [-t] [-g] <slot|index> [slot]\".  The slot number can be any valid game slot, and index can be any valid library preset number.  Use of the -g flag specifies that the first number is a game slot, not a library index.  Use of the -t flag includes the last-modified time in the clipboard export.";
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
				gameSlotToCopy <= MemoryHandler.MaxPresetSlotNum )
			{
				if( MemoryHandler.FoundSavedPresetSigs() )
				{
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
			if( MemoryHandler.FoundDirectPlacementSigs() )
			{
				//	The index we will want to try to place once we find it.
				int libraryIndex = -1;

				//	If argument is in quotes, search for the preset by name.
				if( args.Trim().First() == '"' && args.Trim().Last() == '"' )
				{
					string presetName = args.Trim().Substring( 1, args.Trim().Length - 2 );
					libraryIndex = mConfiguration.PresetLibrary.Presets.FindIndex( ( WaymarkPreset p ) => { return p.Name.Equals( presetName, StringComparison.OrdinalIgnoreCase ); } );
					if( libraryIndex < 0 || libraryIndex >= mConfiguration.PresetLibrary.Presets.Count )
					{
						return $"Unable to find preset \"{presetName}\".";
					}
				}
				//	Otherwise, search by index.
				else if( !int.TryParse( args.Trim(), out libraryIndex ) )
				{
					return $"Invalid preset number \"{args}\".";
				}
				
				//	Try to do the actual placement.
				if( libraryIndex >= 0 && libraryIndex < mConfiguration.PresetLibrary.Presets.Count )
				{
					try
					{
						MemoryHandler.PlacePreset( mConfiguration.PresetLibrary.Presets[libraryIndex].GetAsGamePreset() /*, mConfiguration.AllowClientSidePlacementInOverworldZones*/ );
						return "";
					}
					catch( Exception e )
					{
						PluginLog.Log( $"An unknown error occured while attempting to place preset {libraryIndex} : {e}" );
						return $"An unknown error occured placing preset {libraryIndex}.";
					}
				}
				else
				{
					return $"Invalid preset number \"{libraryIndex}\".";
				}
			}
			else
			{
				return "Unable to place preset; direct placement signatures were not found.  This probably means that the plugin needs to be updated for a new version of FFXIV.";
			}
		}

		protected string ProcessTextCommand_Import( string args )
		{
			uint gameSlotToCopy;
			if( args.Length == 1 &&
				uint.TryParse( args, out gameSlotToCopy ) &&
				gameSlotToCopy >= 1 &&
				gameSlotToCopy <= MemoryHandler.MaxPresetSlotNum )
			{
				if( MemoryHandler.FoundSavedPresetSigs() )
				{
					try
					{
						WaymarkPreset tempPreset = WaymarkPreset.Parse( MemoryHandler.ReadSlot( gameSlotToCopy ) );
						int importedIndex = mConfiguration.PresetLibrary.ImportPreset( tempPreset );
						mConfiguration.Save();
						return $"Imported game preset {gameSlotToCopy} as library preset {importedIndex}.";
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
				return $"An invalid game slot number was provided.";
			}
		}

		protected string ProcessTextCommand_Export( string args )
		{
			var parameters = args.Split();
			bool includeTimestamp = parameters.Contains( "-t" );
			bool useGameSlot = parameters.Contains( "-g" );
			int scratchVal;
			var slotIndexNumbers = parameters.Where( x => int.TryParse( x, out scratchVal ) ).ToList();
			WaymarkPreset presetToExport = null;

			try
			{
				if( slotIndexNumbers.Count < 1 )
				{
					return "No slot or index numbers were provided.";
				}
				else if( slotIndexNumbers.Count == 1 )
				{
					int indexToExport = int.Parse( slotIndexNumbers[0] );
					if( useGameSlot )
					{
						if( indexToExport >= 1 && indexToExport <= MemoryHandler.MaxPresetSlotNum )
						{
							presetToExport = WaymarkPreset.Parse( MemoryHandler.ReadSlot( (uint)indexToExport ) );
						}
						else
						{
							return $"An invalid game slot number ({indexToExport}) was provided.";
						}
					}
					else
					{
						if( indexToExport >= 0 && indexToExport < mConfiguration.PresetLibrary.Presets.Count )
						{
							presetToExport = mConfiguration.PresetLibrary.Presets[indexToExport];
						}
						else
						{
							return $"An invalid library index ({indexToExport}) was provided.";
						}
					}

					string exportStr = "";
					if( includeTimestamp )
					{
						exportStr = JsonConvert.SerializeObject( presetToExport );
					}
					else
					{
						exportStr = WaymarkPresetExport.GetExportString( presetToExport );
					}

					Win32Clipboard.CopyTextToClipboard( exportStr );

					return "Copied to clipboard.";
				}
				else
				{
					int indexToExport = int.Parse( slotIndexNumbers[0] );
					int exportTargetIndex = int.Parse( slotIndexNumbers[1] );
					if( useGameSlot )
					{
						if( indexToExport >= 1 && indexToExport <= MemoryHandler.MaxPresetSlotNum )
						{
							presetToExport = WaymarkPreset.Parse( MemoryHandler.ReadSlot( (uint)indexToExport ) );
						}
						else
						{
							return $"An invalid game slot number to export ({indexToExport}) was provided.";
						}
					}
					else
					{
						if( indexToExport >= 0 && indexToExport < mConfiguration.PresetLibrary.Presets.Count )
						{
							presetToExport = mConfiguration.PresetLibrary.Presets[indexToExport];
						}
						else
						{
							return $"An invalid library index ({indexToExport}) was provided.";
						}
					}

					if( exportTargetIndex >= 1 && exportTargetIndex <= MemoryHandler.MaxPresetSlotNum )
					{
						if( MemoryHandler.WriteSlot( (uint)exportTargetIndex, presetToExport.GetAsGamePreset() ) )
						{
							return $"Preset exported to game slot {exportTargetIndex}.";
						}
						else
						{
							return $"Unable to write to game slot {exportTargetIndex}!";
						}
					}
					else
					{
						return $"An invalid game slot number({exportTargetIndex}) was provided as the target.";
					}
				}
			}
			catch( Exception e )
			{
				PluginLog.Log( $"Unknown error occured while export the preset: {e}" );
				return "Unknown error occured while trying to export the preset.";
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
						str += JsonConvert.SerializeObject( preset ) + "\r\n";
					}
				}
				else
				{
					foreach( var preset in mConfiguration.PresetLibrary.Presets )
					{
						str += WaymarkPresetExport.GetExportString( preset ) + "\r\n";
					}
				}

				Win32Clipboard.CopyTextToClipboard( str );

				return "Waymark library copied to clipboard.";
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
				for( uint i = 1; i <= MemoryHandler.MaxPresetSlotNum; ++i )
				{
					try
					{
						var preset = WaymarkPreset.Parse( MemoryHandler.ReadSlot( i ) );
						if( preset.MapID == prevTerritoryTypeInfo.ContentFinderConditionID && !mConfiguration.PresetLibrary.Presets.Any( x => x.Equals( preset ) ) )
						{
							preset.Name = prevTerritoryTypeInfo.DutyName.ToString() + " - AutoImported";
							mConfiguration.PresetLibrary.ImportPreset( preset );
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
				var presetsToAutoLoad = mConfiguration.PresetLibrary.Presets.Where( x => x.MapID == newTerritoryTypeInfo.ContentFinderConditionID ).Take( MemoryHandler.MaxPresetSlotNum ).ToList();
				for( int i = 0; i < MemoryHandler.MaxPresetSlotNum; ++i )
				{
					GamePreset gamePresetData = new GamePreset();

					if( i < presetsToAutoLoad.Count )
					{
						var preset = presetsToAutoLoad[i];
						gamePresetData = preset.GetAsGamePreset();
					}

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

		public string Name => "WaymarkPresetPlugin";
		protected const string mTextCommandName = "/pwaymark";

		public UInt16 CurrentTerritoryTypeID { get; protected set; }

		protected DalamudPluginInterface mPluginInterface;
		protected ClientState mClientState;
		protected CommandManager mCommandManager;
		protected Condition mCondition;
		protected ChatGui mChatGui;
		protected GameGui mGameGui;
		protected SigScanner mSigScanner;
		protected DataManager mDataManager;
		protected Configuration mConfiguration;
		protected PluginUI mUI;
	}
}
