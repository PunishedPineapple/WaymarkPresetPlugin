using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using CheapLoc;

using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Dalamud.Plugin;

using Newtonsoft.Json;

using static WaymarkPresetPlugin.MathUtils;

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

			//	Localization and Command Initialization
			OnLanguageChanged( mPluginInterface.UiLanguage );

			//	Configuration
			mConfiguration = mPluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
			mConfiguration.Initialize( mPluginInterface );
			MemoryHandler.Init( mSigScanner, mClientState, mCondition );
			ZoneInfoHandler.Init( mDataManager );

			//	UI Initialization
			mUI = new PluginUI( mConfiguration, mPluginInterface, mDataManager, mGameGui, mClientState );

			//	Event Subscription
			mPluginInterface.UiBuilder.Draw += DrawUI;
			mPluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
			mPluginInterface.LanguageChanged += OnLanguageChanged;
			mClientState.TerritoryChanged += OnTerritoryChanged;

			//	IPC
			IpcProvider.RegisterIPC( this, pluginInterface );
		}

		//	Cleanup
		public void Dispose()
		{
			IpcProvider.UnregisterIPC();
			mCommandManager.RemoveHandler( TextCommandName );
			mPluginInterface.LanguageChanged -= OnLanguageChanged;
			mClientState.TerritoryChanged -= OnTerritoryChanged;
			mPluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
			mPluginInterface.UiBuilder.Draw -= DrawUI;
			mUI?.Dispose();
			MemoryHandler.Uninit();
		}

		protected void OnLanguageChanged( string langCode )
		{
			var allowedLang = new List<string>{ "fr", };

			PluginLog.Information( "Trying to set up Loc for culture {0}", langCode );

			if( allowedLang.Contains( langCode ) )
			{
				Loc.Setup( File.ReadAllText( Path.Join( Path.Join( mPluginInterface.AssemblyLocation.DirectoryName, "Resources\\Localization\\" ), $"loc_{langCode}.json" ) ) );
			}
			else
			{
				Loc.SetupWithFallbacks();
			}

			//	Set up the command handler with the current language.
			if( mCommandManager.Commands.ContainsKey( TextCommandName ) )
			{
				mCommandManager.RemoveHandler( TextCommandName );
			}
			mCommandManager.AddHandler( TextCommandName, new CommandInfo( ProcessTextCommand )
			{
				HelpMessage = String.Format( Loc.Localize( "Text Command Description", "Performs waymark preset commands.  Use \"{0}\" for detailed usage information." ), TextCommandName + " " + SubcommandName_Help )
			} );
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
				mUI.LibraryWindow.WindowVisible = !mUI.LibraryWindow.WindowVisible;
			}
			else if( subCommand.ToLower() == SubcommandName_Config )
			{
				mUI.SettingsWindow.WindowVisible = !mUI.SettingsWindow.WindowVisible;
			}
			else if( subCommand.ToLower() == "debug" )
			{
				mUI.DebugWindow.WindowVisible = !mUI.DebugWindow.WindowVisible;
			}
			else if( subCommand.ToLower() == SubcommandName_SlotInfo )
			{
				commandResponse = ProcessTextCommand_SlotInfo( subCommandArgs );
			}
			else if( subCommand.ToLower() == SubcommandName_Place )
			{
				commandResponse = ProcessTextCommand_Place( subCommandArgs );
			}
			else if( subCommand.ToLower() == SubcommandName_Import )
			{
				commandResponse = ProcessTextCommand_Import( subCommandArgs );
			}
			else if( subCommand.ToLower() == SubcommandName_Export )
			{
				commandResponse = ProcessTextCommand_Export( subCommandArgs );
			}
			else if( subCommand.ToLower() == SubcommandName_ExportAll )
			{
				commandResponse = ProcessTextCommand_ExportAll( subCommandArgs );
			}
			else if( subCommand.ToLower() == SubcommandName_QuickPlace )
			{
				commandResponse = ProcessTextCommand_QuickPlace( subCommandArgs );
			}
			else if( subCommand.ToLower() == SubcommandName_Help || subCommand.ToLower() == "?" )
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
			if( args.ToLower() == SubCommandName_Help_Commands )
			{
				return String.Format(	Loc.Localize( "Text Command Response: Help - Subcommands", "Valid commands are as follows: {0}, {1}, {2}, {3}, {4}, and {5}.  If no command is provided, the preset library will be opened.  Type \"{6} <command>\" for detailed subcommand information." ),
										SubcommandName_Place, SubcommandName_Import, SubcommandName_Export, SubcommandName_ExportAll, SubcommandName_SlotInfo, SubcommandName_Config, TextCommandName + " " + SubcommandName_Help );
			}
			else if( args.ToLower() == SubcommandName_Config )
			{
				return Loc.Localize( "Text Command Response: Help - Config", "Opens the settings window." );
			}
			else if( args.ToLower() == SubcommandName_SlotInfo )
			{
				return String.Format(	Loc.Localize( "Text Command Response: Help - Slot Info", "Prints the data saved in the game's slots to the chat window.  Usage: \"{0} <slot>\".  The slot number can be any valid game slot." ),
										TextCommandName + " " + SubcommandName_SlotInfo );
			}
			else if( args.ToLower() == SubcommandName_Place )
			{
				return String.Format(	Loc.Localize( "Text Command Response: Help - Place", "Places the preset with the specified name (if possible).  Quotes MUST be used around the name.  May also specify preset index without quotes instead.  Usage: \"{0} <name>|<index>\".  Name must match exactly (besides case).  Index can be any valid libary preset number." ),
										TextCommandName + " " + SubcommandName_Place );
			}
			else if( args.ToLower() == SubcommandName_Import )
			{
				return String.Format(	Loc.Localize( "Text Command Response: Help - Import", "Copies one of the game's five preset slots to the library.  Usage: \"{0} <slot>\".  The slot number can be any valid game slot.  Command-line import of a formatted preset string is not supported due to length restrictions in the game's chat box." ),
										TextCommandName + " " + SubcommandName_Import );
			}
			else if( args.ToLower() == SubcommandName_Export )
			{
				return String.Format(	Loc.Localize( "Text Command Response: Help - Export", "Copies a preset from the library to the specified game slot *or* copies a preset to the clipboard, depending on flags and parameters.  Usage: \"{0} [{1}] [{2}] <slot|index> [slot]\".  The slot number can be any valid game slot, and index can be any valid library preset number.  Use of the {3} flag specifies that the first number is a game slot, not a library index.  Use of the {4} flag includes the last-modified time in the clipboard export." ),
										TextCommandName + " " + SubcommandName_Export, SubCommandArg_Export_IncludeTime, SubCommandArg_Export_IsGameSlot, SubCommandArg_Export_IsGameSlot, SubCommandArg_Export_IncludeTime );
			}
			else if( args.ToLower() == SubcommandName_ExportAll )
			{
				return String.Format(	Loc.Localize( "Text Command Response: Help - Export All", "Copies all presets in the library to the clipboard, one per line.  Add {0} if you wish to include the last-modified timestamp in the export." ),
										SubCommandArg_Export_IncludeTime );
			}
			else
			{
				return String.Format(	Loc.Localize( "Text Command Response: Help", "Use \"{0}\" to open the GUI.  Use \"{1}\" for a list of text commands." ),
										TextCommandName, TextCommandName + " " + SubcommandName_Help + " " + SubCommandName_Help_Commands );
			}
		}

		protected string ProcessTextCommand_SlotInfo( string args )
		{
			if( args.Length == 1 &&
				uint.TryParse( args, out uint gameSlotToCopy ) &&
				gameSlotToCopy >= 1 &&
				gameSlotToCopy <= MemoryHandler.MaxPresetSlotNum )
			{
				if( MemoryHandler.FoundSavedPresetSigs() )
				{
					try
					{
						WaymarkPreset tempPreset = WaymarkPreset.Parse( MemoryHandler.ReadSlot( gameSlotToCopy ) );
						return String.Format(	Loc.Localize( "Text Command Response: Slot Info - Success 1", "Slot {0} Contents:\r\n{1}" ),
												gameSlotToCopy, tempPreset.GetPresetDataString( mConfiguration.GetZoneNameDelegate, mConfiguration.ShowIDNumberNextToZoneNames ) );
					}
					catch( Exception e )
					{
						PluginLog.Log( $"An unknown error occured while trying to read the game's waymark data:\r\n{e}" );
						return Loc.Localize( "Text Command Response: Slot Info - Error 1", "An unknown error occured while trying to read the game's waymark data." );
					}
				}
				else
				{
					return Loc.Localize( "Text Command Response: Slot Info - Error 2", "Unable to read game's waymark data." );
				}
			}
			else
			{
				return Loc.Localize( "Text Command Response: Slot Info - Error 3", "An invalid game slot number was provided." );
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
					string presetName = args.Trim()[ 1..^1];
					libraryIndex = mConfiguration.PresetLibrary.Presets.FindIndex( ( WaymarkPreset p ) => { return p.Name.Equals( presetName, StringComparison.OrdinalIgnoreCase ); } );
					if( libraryIndex < 0 || libraryIndex >= mConfiguration.PresetLibrary.Presets.Count )
					{
						return String.Format(	Loc.Localize( "Text Command Response: Place - Error 1", "Unable to find preset \"{0}\"." ),
												presetName );
					}
				}
				//	Otherwise, search by index.
				else if( !int.TryParse( args.Trim(), out libraryIndex ) )
				{
					return String.Format(	Loc.Localize( "Text Command Response: Place - Error 2", "Invalid preset number \"{0}\"." ),
											args );
				}
				
				//	Try to do the actual placement.
				if( libraryIndex >= 0 && libraryIndex < mConfiguration.PresetLibrary.Presets.Count )
				{
					if( InternalCommand_PlacePresetByIndex( libraryIndex, false ) )
					{
						return "";
					}
					else
					{
						return String.Format(	Loc.Localize( "Text Command Response: Place - Error 3", "An unknown error occured placing preset {0}." ),
												libraryIndex );
					}
				}
				else
				{
					return String.Format(	Loc.Localize( "Text Command Response: Place - Error 4", "Invalid preset number \"{0}\"." ),
											libraryIndex );
				}
			}
			else
			{
				return Loc.Localize( "Text Command Response: Place - Error 5", "Unable to place preset.  This probably means that the plugin needs to be updated for a new version of FFXIV." );
			}
		}

		protected string ProcessTextCommand_Import( string args )
		{
			if( args.Length == 1 &&
				uint.TryParse( args, out uint gameSlotToCopy ) &&
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
						return String.Format( Loc.Localize( "Text Command Response: Import - Success 1", "Imported game preset {0} as library preset {1}." ),
												gameSlotToCopy, importedIndex );
					}
					catch( Exception e )
					{
						PluginLog.Log( $"An unknown error occured while trying to read the game's waymark data:\r\n{e}" );
						return Loc.Localize( "Text Command Response: Import - Error 1", "An unknown error occured while trying to read the game's waymark data." );
					}
				}
				else
				{
					return Loc.Localize( "Text Command Response: Import - Error 2", "Unable to read game's waymark data.  This probably means that the plugin needs to be updated for a new version of FFXIV." );
				}
			}
			else
			{
				return String.Format( Loc.Localize( "Text Command Response: Import - Error 3", "An invalid game slot number was provided: \"{0}\"." ), args );
			}
		}

		protected string ProcessTextCommand_Export( string args )
		{
			var parameters = args.Split();
			bool includeTimestamp = parameters.Contains( SubCommandArg_Export_IncludeTime );
			bool useGameSlot = parameters.Contains( SubCommandArg_Export_IsGameSlot );
			var slotIndexNumbers = parameters.Where( x => int.TryParse( x, out int scratchVal ) ).ToList();
			WaymarkPreset presetToExport = null;

			try
			{
				if( slotIndexNumbers.Count < 1 )
				{
					return Loc.Localize( "Text Command Response: Export - Error 1", "No slot or index numbers were provided." );
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
							return String.Format(	Loc.Localize( "Text Command Response: Export - Error 2", "An invalid game slot number ({0}) was provided." ),
													indexToExport );
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
							return String.Format(	Loc.Localize( "Text Command Response: Export - Error 3", "An invalid library index ({0}) was provided." ),
													indexToExport );
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

					return Loc.Localize( "Text Command Response: Export - Success 1", "Copied to clipboard." );
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
							return String.Format(	Loc.Localize( "Text Command Response: Export - Error 4", "An invalid game slot number to export ({0}) was provided." ),
													indexToExport );
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
							return String.Format(	Loc.Localize( "Text Command Response: Export - Error 5", "An invalid library index ({0}) was provided." ),
													indexToExport );
						}
					}

					if( exportTargetIndex >= 1 && exportTargetIndex <= MemoryHandler.MaxPresetSlotNum )
					{
						if( MemoryHandler.WriteSlot( (uint)exportTargetIndex, presetToExport.GetAsGamePreset() ) )
						{
							return String.Format(	Loc.Localize( "Text Command Response: Export - Success 2", "Preset exported to game slot {0}." ),
													exportTargetIndex );
						}
						else
						{
							return String.Format(	Loc.Localize( "Text Command Response: Export - Error 6", "Unable to write to game slot {0}!" ),
													exportTargetIndex );
						}
					}
					else
					{
						return String.Format(	Loc.Localize( "Text Command Response: Export - Error 7", "An invalid game slot number ({0}) was provided as the target." ),
												exportTargetIndex );
					}
				}
			}
			catch( Exception e )
			{
				PluginLog.Log( $"Unknown error occured while export the preset:\r\n{e}" );
				return Loc.Localize( "Text Command Response: Export - Error 8", "An unknown error occured while trying to export the preset." );
			}
		}

		protected string ProcessTextCommand_ExportAll( string args )
		{
			try
			{
				string str = "";
				if( args.ToLower().Trim() == SubCommandArg_Export_IncludeTime )
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

				return Loc.Localize( "Text Command Response: Export All - Success 1", "Waymark library copied to clipboard." );
			}
			catch( Exception e )
			{
				PluginLog.Log( $"Unknown error occured while trying to copy presets to clipboard:\r\n{e}" );
				return Loc.Localize( "Text Command Response: Export All - Error 1", "An unknown error occured while trying to copy presets to clipboard." );
			}
		}

		protected string ProcessTextCommand_QuickPlace( string args )
        {
            if (MemoryHandler.FoundDirectPlacementSigs())
            {
                var tempPreset = new WaymarkPreset();
                var shape = args.Trim().ToLower();


                //TODO: Use player spawn coordinates to compute arena center
                var arenaCenter = new Vector3(100, 0, 100);
				Vector3[] points;

                switch (shape)
                {
                    case "circle":
                        points = ComputeRadialPositions(arenaCenter, 16, 8, 0);
                        for (int i = 0; i < 8; ++i)
                        {
                            tempPreset[i].Active = true;
                            tempPreset[i].SetCoords(points[i]);
                        }
                        break;
                    case "circleb":
                        points = ComputeRadialPositions(arenaCenter, 16, 8, 0);
                        for (int i = 0; i < 8; ++i)
                        {
                            var i2 = Mod2(i * 2, 7);
                            tempPreset[i].Active = true;
                            tempPreset[i].SetCoords(points[i2]);
                        }
                        break;
                    case "square":
						points = ComputeSquarePositions(arenaCenter, 16);
                        for (int i = 0; i < 8; ++i)
                        {
                            tempPreset[i].Active = true;
                            tempPreset[i].SetCoords(points[i]);
                        }
                        break;
					case "squareb":
                        points = ComputeSquarePositions(arenaCenter, 16);
                        for (int i = 0; i < 8; ++i)
                        {
                            var i2 = Mod2(i * 2, 7);
                            tempPreset[i].Active = true;
                            tempPreset[i].SetCoords(points[i2]);
                        }
                        break;
					case "pfline":
						{
							var start = arenaCenter + new Vector3(-8, 0, 18);
							for (int i = 0; i < 8; ++i)
							{
								tempPreset[i].Active = true;
								tempPreset[i].SetCoords(start + new Vector3(2.3f, 0, 0) * i);
							}
						}
						break;
					case "pfquickmarch":
						{
							var startA = arenaCenter + new Vector3(-1.5f, 0, 14);
							var startB = arenaCenter + new Vector3(1.5f, 0, 14);
                            for (int i = 0; i < 4; ++i)
                            {
                                tempPreset[i].Active = true;
                                tempPreset[i].SetCoords(startA + new Vector3(0, 0, 1.7f) * i);

                                tempPreset[i + 4].Active = true;
                                tempPreset[i + 4].SetCoords(startB + new Vector3(0, 0, 1.7f) * i);
                            }
                        }
						break;
                    default:
						return $"Unknown shape \"{shape}\" specified";
				}

                MemoryHandler.PlacePreset(tempPreset.GetAsGamePreset());
            }
            else
            {
                return Loc.Localize("Text Command Response: Place - Error 5", "Unable to place preset.  This probably means that the plugin needs to be updated for a new version of FFXIV.");
            }



            return null;
		}

		internal List<int> InternalCommand_GetPresetsForContentFinderCondition( UInt16 contentFinderCondition )
		{
			List<int> foundPresets = new();

			if( contentFinderCondition != 0 )
			{
				for( int i = 0; i < mConfiguration.PresetLibrary.Presets.Count; ++i )
				{
					if( mConfiguration.PresetLibrary.Presets[i].MapID == contentFinderCondition ) foundPresets.Add( i );
				}
			}

			return foundPresets;
		}

		internal List<int> InternalCommand_GetPresetsForTerritoryType( UInt32 territoryType )
		{
			UInt16 contentFinderCondition = ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( territoryType );
			return InternalCommand_GetPresetsForContentFinderCondition( contentFinderCondition );
		}

		internal List<int> InternalCommand_GetPresetsForCurrentArea()
		{
			return InternalCommand_GetPresetsForTerritoryType( mClientState.TerritoryType );
		}

		internal bool InternalCommand_PlacePresetByIndex( int index, bool requireZoneMatch = true )
		{
			if( !MemoryHandler.FoundDirectPlacementSigs() ) return false;
			if( index < 0 || index >= mConfiguration.PresetLibrary.Presets.Count ) return false;
			if( requireZoneMatch && mConfiguration.PresetLibrary.Presets[index].MapID != ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( mClientState.TerritoryType ) ) return false;

			try
			{
				MemoryHandler.PlacePreset( mConfiguration.PresetLibrary.Presets[index].GetAsGamePreset() );
				return true;
			}
			catch( Exception e )
			{
				PluginLog.Log( $"An unknown error occured while attempting to place preset {index}:\r\n{e}" );
				return false;
			}
		}

		internal bool InternalCommand_PlacePresetByName( string name, bool requireZoneMatch = true )
		{
			int libraryIndex = mConfiguration.PresetLibrary.Presets.FindIndex( p =>
			{
				return p.Name.Equals( name, StringComparison.OrdinalIgnoreCase );
			} );

			if( libraryIndex < 0 )
			{
				return false;
			}
			else
			{
				return InternalCommand_PlacePresetByIndex( libraryIndex, requireZoneMatch );
			}
		}

		internal bool InternalCommand_PlacePresetByNameAndContentFinderCondition( string name, UInt16 contentFinderCondition )
		{
			if( contentFinderCondition == 0 ) return false;

			int libraryIndex = mConfiguration.PresetLibrary.Presets.FindIndex( p =>
			{
				return	p.MapID == contentFinderCondition &&
						p.Name.Equals( name, StringComparison.OrdinalIgnoreCase );
			} );

			if( libraryIndex < 0 )
			{
				return false;
			}
			else
			{
				return InternalCommand_PlacePresetByIndex( libraryIndex );
			}
		}

		internal bool InternalCommand_PlacePresetByNameAndTerritoryType( string name, UInt32 territoryType )
		{
			UInt16 contentFinderCondition = ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( territoryType );
			return InternalCommand_PlacePresetByNameAndContentFinderCondition( name, contentFinderCondition );
		}

		internal string GetLibraryPresetName( int index )
		{
			if( index < 0 || index >= mConfiguration.PresetLibrary.Presets.Count ) return null;
			else return mConfiguration.PresetLibrary.Presets[index].Name;
		}

		protected void DrawUI()
		{
			mUI.Draw();
		}

		protected void DrawConfigUI()
		{
			mUI.SettingsWindow.WindowVisible = true;
		}

		protected void OnTerritoryChanged( object sender, UInt16 ID )
		{
			ZoneInfo prevTerritoryTypeInfo = ZoneInfoHandler.GetZoneInfoFromTerritoryTypeID( CurrentTerritoryTypeID );
			ZoneInfo newTerritoryTypeInfo = ZoneInfoHandler.GetZoneInfoFromTerritoryTypeID( ID );
			CurrentTerritoryTypeID = ID;

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
						PluginLog.Log( $"Error while attempting to auto-import game slot {i}:\r\n{e}" );
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
					GamePreset gamePresetData = new();

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
						PluginLog.Log( $"Error while auto copying preset data to game slot {i}:\r\n{e}" );
					}
				}
			}
		}

		internal const string InternalName = "WaymarkPresetPlugin";
		public string Name => "Waymark Preset Plugin";
		internal static string TextCommandName => "/pwaymark";
		internal static string SubcommandName_Config => "config";
		internal static string SubcommandName_SlotInfo => "slotinfo";
		internal static string SubcommandName_Place => "place";
		internal static string SubcommandName_Import => "import";
		internal static string SubcommandName_Export => "export";
		internal static string SubcommandName_ExportAll => "exportall";
		internal static string SubcommandName_Help => "help";
		internal static string SubcommandName_QuickPlace => "quickplace";
		internal static string SubCommandName_Help_Commands => "commands";
		internal static string SubCommandArg_Export_IncludeTime => "-t";
		internal static string SubCommandArg_Export_IsGameSlot => "-g";

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
