using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using Lumina.Excel;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Utility;
using Dalamud.Logging;
using Dalamud.Interface;
using Dalamud.Game.Command;
using ImGuiScene;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.IO;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace WaymarkPresetPlugin
{
	// It is good to have this be disposable in general, in case you ever need it
	// to do any cleanup
	public class PluginUI : IDisposable
	{
		//	Construction
		public PluginUI( Configuration configuration, DalamudPluginInterface pluginInterface, DataManager dataManager, CommandManager commandManager, GameGui gameGui )
		{
			mConfiguration = configuration;
			mPluginInterface = pluginInterface;
			mDataManager = dataManager;
			mCommandManager = commandManager;
			mGameGui = gameGui;
			mpLibraryPresetDragAndDropData = Marshal.AllocHGlobal( sizeof( int ) );
			mpEditWaymarkDragAndDropData = Marshal.AllocHGlobal( sizeof( int ) );
			if( mpLibraryPresetDragAndDropData == IntPtr.Zero ||
				mpEditWaymarkDragAndDropData == IntPtr.Zero )
			{
				throw new Exception( "Error in PluginUI constructor: Unable to allocate memory for drag and drop info." );
			}

			try
			{
				//	Try to read in the view state data.
				string viewStateDataFilePath = Path.Join( mPluginInterface.GetPluginConfigDirectory(), $"\\MapViewStateData_v1.json" );
				string jsonStr = File.ReadAllText( viewStateDataFilePath );
				var viewData = JsonConvert.DeserializeObject<Dictionary<uint, MapViewState>>( jsonStr );
				if( viewData != null ) MapViewStateData = viewData;
			}
			catch( Exception e )
			{
				PluginLog.LogWarning( $"Unable to load map view state data: {e}" );
			}
		}

		//	Destruction
		public void Dispose()
		{
			//	Try to save off the view state data.
			try
			{
				string jsonStr = JsonConvert.SerializeObject( MapViewStateData, Formatting.Indented );
				string viewStateDataFilePath = Path.Join( mPluginInterface.GetPluginConfigDirectory(), $"\\MapViewStateData_v1.json" );
				File.WriteAllText( viewStateDataFilePath, jsonStr );
			}
			catch( Exception e )
			{
				PluginLog.LogWarning( $"Unable to save map view state data: {e}" );
			}

			//	Try to do this nicely for a moment, but then just brute force it to clean up as much as we can.
			mMapTextureDictMutex.WaitOne( 500 );

			//	Clean up all of the map textures that we've loaded.
			foreach( var mapTexturesList in MapTextureDict )
			{
				foreach( var tex in mapTexturesList.Value )
				{
					tex?.Dispose();
				}
			}

			//	Release the mutex and dispose of it.
			mMapTextureDictMutex.ReleaseMutex();
			mMapTextureDictMutex.Dispose();

			//	Free the drag and drop data.
			Marshal.FreeHGlobal( mpLibraryPresetDragAndDropData );
			Marshal.FreeHGlobal( mpEditWaymarkDragAndDropData );
		}

		public void Initialize()
		{
			mWaymarkIconTextures[0] ??= mDataManager.GetImGuiTextureIcon( 61241 );	//A
			mWaymarkIconTextures[1] ??= mDataManager.GetImGuiTextureIcon( 61242 );	//B
			mWaymarkIconTextures[2] ??= mDataManager.GetImGuiTextureIcon( 61243 );	//C
			mWaymarkIconTextures[3] ??= mDataManager.GetImGuiTextureIcon( 61247 );	//D
			mWaymarkIconTextures[4] ??= mDataManager.GetImGuiTextureIcon( 61244 );	//1
			mWaymarkIconTextures[5] ??= mDataManager.GetImGuiTextureIcon( 61245 );	//2
			mWaymarkIconTextures[6] ??= mDataManager.GetImGuiTextureIcon( 61246 );	//3
			mWaymarkIconTextures[7] ??= mDataManager.GetImGuiTextureIcon( 61248 );	//4
		}

		public void Draw()
		{
			//	Draw the sub-windows.
			DrawMainWindow();
			DrawInfoWindow();
			DrawMapWindow();
			DrawEditorWindow();
			DrawSettingsWindow();
			if( GimpedModeWarningWindowVisible ) DrawGimpedModeWarningWindow();
		}

		protected void DrawGimpedModeWarningWindow()
		{
			ImGuiHelpers.ForceNextWindowMainViewport();
			ImGuiHelpers.SetNextWindowPosRelativeMainViewport( ImGuiHelpers.MainViewport.Size / 2f - ImGuiHelpers.MainViewport.Size / 10f, ImGuiCond.Appearing );
			ImGui.SetNextWindowSize( ImGuiHelpers.MainViewport.Size / 5f );
			if( ImGui.Begin( "WARNING###WarningMissingSignaturesGimpedMode", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize ) )
			{
				ImGui.PushTextWrapPos( ImGuiHelpers.MainViewport.Size.X / 5.1f );
				ImGui.Text( "One or more function signatures used by WaymarkPresetPlugin could not be found.  " +
							"This probably means that FFXIV has been updated in a way that partially breaks this plugin.  " +
							"If FFXIV has not recently been patched, please file a bug report on Github for this plugin.  " +
							"You should be able to continue using the plugin to import presets from, and export them to " +
							"the game's waymark preset window (you will have to close and reopen the window to see any " +
							"exports)." );
				ImGui.PopTextWrapPos();

				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();

				if( ImGui.Button( "Ok" ) )
				{
					GimpedModeWarningWindowVisible = false;
					HaveShownGimpedModeWarningMessage = true;
				}
			}

			ImGui.End();
		}

		protected void DrawMainWindow()
		{
			//	Handle game window docking stuff.
			Vector2 dockedWindowPos = Vector2.Zero;
			bool fieldMarkerAddonVisible = false;
			unsafe
			{
				var pFieldMarkerAddon = (AtkUnitBase*)mGameGui.GetAddonByName( "FieldMarker", 1 );
				if( pFieldMarkerAddon != null && pFieldMarkerAddon->IsVisible && pFieldMarkerAddon->RootNode != null )
				{
					fieldMarkerAddonVisible = true;
					dockedWindowPos.X = pFieldMarkerAddon->X + pFieldMarkerAddon->RootNode->Width * pFieldMarkerAddon->Scale;
					dockedWindowPos.Y = pFieldMarkerAddon->Y;
				}
			}

			if( mConfiguration.OpenAndCloseWithFieldMarkerAddon && FieldMarkerAddonWasOpen && !fieldMarkerAddonVisible )
			{
				MainWindowVisible = false;
			}
			else if( mConfiguration.OpenAndCloseWithFieldMarkerAddon && !FieldMarkerAddonWasOpen && fieldMarkerAddonVisible )
			{
				MainWindowVisible = true;
			}
			FieldMarkerAddonWasOpen = fieldMarkerAddonVisible;

			if( !MainWindowVisible )
			{
				return;
			}

			//	Draw the window.
			if( mConfiguration.AttachLibraryToFieldMarkerAddon && fieldMarkerAddonVisible ) ImGui.SetNextWindowPos( dockedWindowPos );
			ImGui.SetNextWindowSize( new Vector2( 375, 375 ) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 375 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( "Waymark Library", ref mMainWindowVisible, ImGuiWindowFlags.NoCollapse ) )
			{
				/*if( ImGui.Button( "A" ) )
				{
					mCommandManager.ProcessCommand( "/waymark a" );
				}
				ImGui.SameLine();
				if( ImGui.Button( "B" ) )
				{
					mCommandManager.ProcessCommand( "/waymark b" );
				}
				ImGui.SameLine();
				if( ImGui.Button( "C" ) )
				{
					mCommandManager.ProcessCommand( "/waymark c" );
				}
				ImGui.SameLine();
				if( ImGui.Button( "D" ) )
				{
					mCommandManager.ProcessCommand( "/waymark d" );
				}
				ImGui.SameLine();
				if( ImGui.Button( "1" ) )
				{
					mCommandManager.ProcessCommand( "/waymark 1" );
				}
				ImGui.SameLine();
				if( ImGui.Button( "2" ) )
				{
					mCommandManager.ProcessCommand( "/waymark 2" );
				}
				ImGui.SameLine();
				if( ImGui.Button( "3" ) )
				{
					mCommandManager.ProcessCommand( "/waymark 3" );
				}
				ImGui.SameLine();
				if( ImGui.Button( "4" ) )
				{
					mCommandManager.ProcessCommand( "/waymark 4" );
				}*/

				bool previouslyFilteredOnZone = mConfiguration.FilterOnCurrentZone;
				ImGui.Checkbox( "Filter on Current Zone", ref mConfiguration.mFilterOnCurrentZone );
				if( mConfiguration.FilterOnCurrentZone != previouslyFilteredOnZone ) mConfiguration.Save();	//	I'd rather just save the state when the plugin is unloaded, but that's not been feasible in the past.
				if( MemoryHandler.FoundDirectSaveSigs() )
				{
					ImGui.SameLine( ImGui.GetWindowWidth() - 163 * ImGui.GetIO().FontGlobalScale );  //*****TODO: The magic number is cheap and hacky; actually get the button width if we can.*****
					if( ImGui.Button( "Save Current Waymarks" ) )
					{
						GamePreset currentWaymarks = new GamePreset();
						if( MemoryHandler.GetCurrentWaymarksAsPresetData( ref currentWaymarks ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( currentWaymarks ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
					}
				}
				int indexToMove = -1;
				int indexToMoveTo = -1;
				bool moveToAfter = false;
				ImGui.BeginGroup();
				if( mConfiguration.PresetLibrary.Presets.Count > 0 )
				{
					if( mConfiguration.mSortPresetsByZone )
					{
						var dict = mConfiguration.PresetLibrary.GetSortedIndices();
						foreach( var zone in dict )
						{
							if( !mConfiguration.FilterOnCurrentZone || zone.Key == ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( CurrentTerritoryTypeID ) )
							{
								if( ImGui.CollapsingHeader( ZoneInfoHandler.GetZoneInfoFromContentFinderID( zone.Key ).DutyName.ToString() ) )
								{
									var indices = zone.Value;
									for( int i = 0; i < indices.Count; ++i )
									{
										if( ImGui.Selectable( $"{mConfiguration.PresetLibrary.Presets[indices[i]].Name}{(mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + indices[i].ToString() + ")" : "")}###_Preset_{indices[i]}", indices[i] == SelectedPreset ) )
										{
											//	It's probably a bad idea to allow the selection to change when a preset's being edited.
											if( !EditingPreset )
											{
												if( mConfiguration.AllowUnselectPreset && indices[i] == SelectedPreset )
												{
													SelectedPreset = -1;
												}
												else
												{
													SelectedPreset = indices[i];
												}

												WantToDeleteSelectedPreset = false;
											}
										}
										if( !EditingPreset && ImGui.BeginDragDropSource( ImGuiDragDropFlags.None ) )
										{
											ImGui.SetDragDropPayload( $"PresetIdxZ{zone.Key}", mpLibraryPresetDragAndDropData, sizeof( int ) );
											Marshal.WriteInt32( mpLibraryPresetDragAndDropData, indices[i] );
											ImGui.Text( $"Moving: {mConfiguration.PresetLibrary.Presets[indices[i]].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + indices[i].ToString() + ")" : "" )}" );
											ImGui.EndDragDropSource();
										}
										if( !EditingPreset && ImGui.BeginDragDropTarget() )
										{
											unsafe
											{
												ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"PresetIdxZ{zone.Key}", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect );
												if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
												{
													if( payload.IsDelivery() )
													{
														indexToMove = Marshal.ReadInt32( payload.Data );
														indexToMoveTo = indices[i];
													}
													else
													{
														ImGuiUtils.AddOverline( new Vector4( 1.0f, 1.0f, 0.0f, 1.0f ), 3.0f );
													}
												}
											}
											ImGui.EndDragDropTarget();
										}
									}
									unsafe
									{
										if( !EditingPreset && ImGui.GetDragDropPayload().NativePtr != null && ImGui.GetDragDropPayload().Data != IntPtr.Zero )
										{
											int draggedIndex = Marshal.ReadInt32( mpLibraryPresetDragAndDropData );
											if( draggedIndex >= 0 && draggedIndex < mConfiguration.PresetLibrary.Presets.Count && mConfiguration.PresetLibrary.Presets[draggedIndex].MapID == zone.Key )
											{
												ImGui.Selectable( "<Move To Bottom>" );
												if( ImGui.BeginDragDropTarget() )
												{
													ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"PresetIdxZ{zone.Key}", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect );
													if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
													{
														if( payload.IsDelivery() )
														{
															indexToMove = Marshal.ReadInt32( payload.Data );
															indexToMoveTo = indices.Last();
															moveToAfter = true;
														}
														else
														{
															ImGuiUtils.AddOverline( new Vector4( 1.0f, 1.0f, 0.0f, 1.0f ), 3.0f );
														}
													}
													ImGui.EndDragDropTarget();
												}
											}
										}
									}
								}
							}
						}
					}
					else
					{
						if( ImGui.CollapsingHeader( "Presets" ) )
						{
							for( int i = 0; i < mConfiguration.PresetLibrary.Presets.Count; ++i )
							{
								if( !mConfiguration.FilterOnCurrentZone || mConfiguration.PresetLibrary.Presets[i].MapID == ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( CurrentTerritoryTypeID ) )
								{
									if( ImGui.Selectable( $"{mConfiguration.PresetLibrary.Presets[i].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + i.ToString() + ")" : "" )}###_Preset_{i}", i == SelectedPreset ) )
									{
										//	It's probably a bad idea to allow the selection to change when a preset's being edited.
										if( !EditingPreset )
										{
											if( mConfiguration.AllowUnselectPreset && i == SelectedPreset )
											{
												SelectedPreset = -1;
											}
											else
											{
												SelectedPreset = i;
											}

											WantToDeleteSelectedPreset = false;
										}
									}
									if( !EditingPreset && ImGui.BeginDragDropSource( ImGuiDragDropFlags.None ) )
									{
										ImGui.SetDragDropPayload( $"PresetIdxAnyZone", mpLibraryPresetDragAndDropData, sizeof( int ) );
										Marshal.WriteInt32( mpLibraryPresetDragAndDropData, i );
										ImGui.Text( $"Moving: {mConfiguration.PresetLibrary.Presets[i].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + i.ToString() + ")" : "" )}" );
										ImGui.EndDragDropSource();
									}
									if( !EditingPreset && ImGui.BeginDragDropTarget() )
									{
										unsafe
										{
											ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"PresetIdxAnyZone", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect );
											if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
											{
												if( payload.IsDelivery() )
												{
													indexToMove = Marshal.ReadInt32( payload.Data );
													indexToMoveTo = i;
												}
												else
												{
													ImGuiUtils.AddOverline( new Vector4( 1.0f, 1.0f, 0.0f, 1.0f ), 3.0f );
												}
											}
										}
										ImGui.EndDragDropTarget();
									}
								}
							}
							unsafe
							{
								if( !EditingPreset && ImGui.GetDragDropPayload().NativePtr != null && ImGui.GetDragDropPayload().Data != IntPtr.Zero )
								{
									int draggedIndex = Marshal.ReadInt32( mpLibraryPresetDragAndDropData );
									if( draggedIndex >= 0 && draggedIndex < mConfiguration.PresetLibrary.Presets.Count )
									{
										ImGui.Selectable( "<Move To Bottom>" );
										if( ImGui.BeginDragDropTarget() )
										{
											ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"PresetIdxAnyZone", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect );
											if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
											{
												if( payload.IsDelivery() )
												{
													indexToMove = Marshal.ReadInt32( payload.Data );
													indexToMoveTo = mConfiguration.PresetLibrary.Presets.Count;
												}
												else
												{
													ImGuiUtils.AddOverline( new Vector4( 1.0f, 1.0f, 0.0f, 1.0f ), 3.0f );
												}
											}
											ImGui.EndDragDropTarget();
										}
									}
								}
							}
						}
					}
				}
				else
				{
					ImGui.Text( "Preset library empty!" );
				}
				ImGui.EndGroup();

				//	Input box for import.
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				if( ImGui.CollapsingHeader( "Import Options" ) )
				{
					ImGui.BeginGroup(); //Buttons don't seem to work under a header without being in a group.
					ImGui.InputTextWithHint( "##JSONImportTextBox", "Paste a preset here and click \"Import\".", ref mPresetImportString, 1024 );	//Most exports max out around 500 characters with all waymarks, so this leaves heaps of room for a long name.
					ImGui.SameLine();
					if( ImGui.Button( "Import" ) )
					{
						if( mConfiguration.PresetLibrary.ImportPreset( PresetImportString ) >= 0 )
						{
							PresetImportString = "";
							mConfiguration.Save();
						}
					}
					if( MemoryHandler.FoundSavedPresetSigs() )
					{
						//ImGui.SameLine();
						ImGui.Text( "Or import from game slot: " );
						ImGui.SameLine();
						if( ImGui.Button( "1" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( MemoryHandler.ReadSlot( 1 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
						ImGui.SameLine();
						if( ImGui.Button( "2" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( MemoryHandler.ReadSlot( 2 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
						ImGui.SameLine();
						if( ImGui.Button( "3" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( MemoryHandler.ReadSlot( 3 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
						ImGui.SameLine();
						if( ImGui.Button( "4" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( MemoryHandler.ReadSlot( 4 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
						ImGui.SameLine();
						if( ImGui.Button( "5" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( MemoryHandler.ReadSlot( 5 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
					}
					try
					{
						ImGui.PushStyleColor( ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] );
						ImGui.PushFont( UiBuilder.IconFont );
						ImGui.Text( "\uF0C1" );
						ImGui.PopFont();
						ImGui.PopStyleColor();
						ImGui.SameLine();
						ImGuiUtils.URLLink( "https://github.com/PunishedPineapple/WaymarkPresetPlugin/wiki/Preset-Resources", "Where to find importable presets", false, UiBuilder.IconFont );
					}
					catch( Exception e )
					{
						PluginLog.LogWarning( $"Unable to open the requested link:\r\n{e}" );
					}
					ImGui.EndGroup();
				}
				if( ImGui.CollapsingHeader( "Export/Backup Options" ) )
				{
					ImGui.BeginGroup(); //Buttons don't seem to work under a header without being in a group.
					if( ImGui.Button( "Export All Presets to Clipboard" ) )
					{
						try
						{
							string str = "";
							foreach( var preset in mConfiguration.PresetLibrary.Presets )
							{
								str += WaymarkPresetExport.GetExportString( preset ) + "\r\n";
							}
							Win32Clipboard.CopyTextToClipboard( str );
						}
						catch( Exception e )
						{
							PluginLog.Log( $"Error while exporting all presets: {e}" );
						}
					}
					if( ImGui.Button( "Backup Current Config" ) )
					{
						mConfiguration.BackupConfigFile();
					}
					ImGuiUtils.HelpMarker( "Copies the current config file to a backup folder in the Dalamud \"pluginConfigs\" directory." );
					ImGui.EndGroup();
				}

				//	Handle moving a preset now if the user wanted to.
				if( indexToMove >= 0 && indexToMoveTo >= 0 )
				{
					SelectedPreset = mConfiguration.PresetLibrary.MovePreset( indexToMove, indexToMoveTo, moveToAfter );
					if( SelectedPreset == -1 )
					{
						PluginLog.LogDebug( $"Unable to move preset {indexToMove} to {( moveToAfter ? "after " : "" )}index {indexToMoveTo}." );
					}
					else
					{
						PluginLog.LogDebug( $"Moved preset {indexToMove} to index {SelectedPreset}." );
						mConfiguration.Save();
					}
					Marshal.WriteInt32( mpLibraryPresetDragAndDropData, -1 );
				}
			}

			//	Store the position and size so that we can keep the companion info window in the right place.
			MainWindowPos = ImGui.GetWindowPos();
			MainWindowSize = ImGui.GetWindowSize();

			//	We're done.
			ImGui.End();
		}

		protected void DrawInfoWindow()
		{
			if( !MainWindowVisible || ( SelectedPreset < 0 && !mConfiguration.AlwaysShowInfoPane ) )
			{
				return;
			}

			ImGui.SetNextWindowSize( new Vector2( 250, 375 ) * ImGui.GetIO().FontGlobalScale);
			ImGui.SetNextWindowPos( new Vector2( MainWindowPos.X + MainWindowSize.X, MainWindowPos.Y ) );	//Note that this does *not* need to be viewport-relative, since it is just an offset relative to the library window.
			if( ImGui.Begin( "Preset Info", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar ) )
			{
				if( SelectedPreset >= 0 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
				{
					ImGui.BeginGroup();
					ImGui.Text( "Copy to slot:" );
					ImGui.SameLine();
					ImGui.BeginGroup();
					if( ImGui.Button( "1" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[SelectedPreset], 1u );
					}
					ImGui.SameLine();
					if( ImGui.Button( "2" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[SelectedPreset], 2u );
					}
					ImGui.SameLine();
					if( ImGui.Button( "3" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[SelectedPreset], 3u );
					}
					ImGui.SameLine();
					if( ImGui.Button( "4" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[SelectedPreset], 4u );
					}
					ImGui.SameLine();
					if( ImGui.Button( "5" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[SelectedPreset], 5u );
					}
					if( MemoryHandler.FoundDirectPlacementSigs() )
					{
						ImGui.SameLine();
						if( ImGui.Button( "Place" ) )
						{
							MemoryHandler.PlacePreset( mConfiguration.PresetLibrary.Presets[SelectedPreset].GetAsGamePreset() /*, mConfiguration.AllowClientSidePlacementInOverworldZones*/ );
						}
					}

					ImGui.EndGroup();
					ImGui.Text( "Preset Info:" );
					ImGui.SameLine( ImGui.GetWindowWidth() - mButtonMapViewWidth - mRightAlignPadding );
					if( ImGui.Button( "Map View" ) )
					{
						MapWindowVisible = !MapWindowVisible;
					}
					mButtonMapViewWidth = ImGui.GetItemRectSize().X;

					if( ImGui.BeginTable( "###PresetInfoPaneWaymarkDataTable", 4 ) )
					{
						ImGui.TableSetupColumn( "Waymark", ImGuiTableColumnFlags.WidthFixed, 15 * ImGui.GetIO().FontGlobalScale );
						ImGui.TableSetupColumn( "X", ImGuiTableColumnFlags.WidthStretch );
						ImGui.TableSetupColumn( "Y", ImGuiTableColumnFlags.WidthStretch );
						ImGui.TableSetupColumn( "Z", ImGuiTableColumnFlags.WidthStretch );
						for( int i = 0; i < 8; ++i )
						{
							var waymark = mConfiguration.PresetLibrary.Presets[SelectedPreset][i];
							ImGui.TableNextRow();
							ImGui.TableSetColumnIndex( 0 );
							ImGui.Text( $"{mConfiguration.PresetLibrary.Presets[SelectedPreset].GetNameForWaymarkIndex( i )}:" );
							ImGui.TableSetColumnIndex( 1 );
							ImGuiUtils.RightAlignTableText( waymark.Active ? waymark.X.ToString( "0.00" ) : "Unused" );
							ImGui.TableSetColumnIndex( 2 );
							ImGuiUtils.RightAlignTableText( waymark.Active ? waymark.Y.ToString( "0.00" ) : " " );
							ImGui.TableSetColumnIndex( 3 );
							ImGuiUtils.RightAlignTableText( waymark.Active ? waymark.Z.ToString( "0.00" ) : " " );
						}
						ImGui.EndTable();
					}

					string zoneStr = ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID ).DutyName;
					zoneStr += mConfiguration.ShowIDNumberNextToZoneNames ? $" ({mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID})" : "";
					ImGui.Text( $"Zone: {zoneStr}" );
					ImGui.Text( $"Last Modified: {mConfiguration.PresetLibrary.Presets[SelectedPreset].Time.LocalDateTime}" );

					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					if( ImGui.Button( "Export to Clipboard" ) )
					{
						if( SelectedPreset >= 0 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
						{
							Win32Clipboard.CopyTextToClipboard( WaymarkPresetExport.GetExportString( mConfiguration.PresetLibrary.Presets[SelectedPreset] ) );
						}
					}
					ImGui.SameLine();
					if( ImGui.Button( "Edit" ) && EditingPresetIndex == -1 )  //Don't want to let people start editing while the edit window is already open.
					{
						EditingPresetIndex = SelectedPreset;
						ScratchEditingPreset = new ScratchPreset( mConfiguration.PresetLibrary.Presets[EditingPresetIndex] );
					}
					ImGui.SameLine();
					ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
					if( ImGui.Button( "Delete" ) && EditingPresetIndex == -1 )
					{
						WantToDeleteSelectedPreset = true;
					}
					if( WantToDeleteSelectedPreset )
					{
						ImGui.Text( "Confirm delete: " );
						ImGui.SameLine();
						if( ImGui.Button( "Yes" ) )
						{
							mConfiguration.PresetLibrary.DeletePreset( SelectedPreset );
							WantToDeleteSelectedPreset = false;
							if( SelectedPreset == EditingPresetIndex ) EditingPresetIndex = -1;
							SelectedPreset = -1;
							mConfiguration.Save();
						}
						ImGui.PushStyleColor( ImGuiCol.Text, 0xffffffff );
						ImGui.SameLine();
						if( ImGui.Button( "No" ) )
						{
							WantToDeleteSelectedPreset = false;
						}
						ImGui.PopStyleColor();
					}
					ImGui.PopStyleColor();
				}
				else
				{
					ImGui.Text( "No preset selected." );
				}
			}

			ImGui.End();
		}

		protected void DrawEditorWindow()
		{
			if( EditingPresetIndex < 0 || EditingPresetIndex >= mConfiguration.PresetLibrary.Presets.Count )
			{
				return;
			}

			if( ImGui.Begin( "Preset Editor", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize ) )
			{
				if( ScratchEditingPreset != null )
				{
					ImGui.Text( "Name: " );
					ImGui.SameLine();
					ImGui.InputText( "##PresetName", ref ScratchEditingPreset.Name, 128 );
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.BeginGroup();
					if( ImGui.BeginTable( "###PresetEditorWaymarkTable", 4 ) )
					{
						ImGui.TableSetupColumn( "Active", ImGuiTableColumnFlags.WidthStretch );
						ImGui.TableSetupColumn( "X", ImGuiTableColumnFlags.WidthStretch );
						ImGui.TableSetupColumn( "Y", ImGuiTableColumnFlags.WidthStretch );
						ImGui.TableSetupColumn( "Z", ImGuiTableColumnFlags.WidthStretch );
						ImGui.TableHeadersRow();
						
						foreach( var waymark in ScratchEditingPreset.Waymarks )
						{
							ImGui.TableNextRow();
							ImGui.TableSetColumnIndex( 0 );
							ImGui.Checkbox( $"{waymark.Label}             ###{waymark.Label}", ref waymark.Active );
							if( ImGui.BeginDragDropSource( ImGuiDragDropFlags.None ) )
							{
								ImGui.SetDragDropPayload( $"EditPresetWaymark", mpEditWaymarkDragAndDropData, sizeof( int ) );
								Marshal.WriteInt32( mpEditWaymarkDragAndDropData, waymark.ID );
								ImGui.Text( $"Swap Waymark {waymark.Label} with..." );
								ImGui.EndDragDropSource();
							}
							if( ImGui.BeginDragDropTarget() )
							{
								unsafe
								{
									ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"EditPresetWaymark", ImGuiDragDropFlags.None );
									if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
									{
										ScratchEditingPreset.SwapWaymarks( waymark.ID, Marshal.ReadInt32( payload.Data ) );
									}
								}
								ImGui.EndDragDropTarget();
							}
							ImGui.TableSetColumnIndex( 1 );
							ImGui.InputFloat( $"##{waymark.Label}-X", ref waymark.X );
							ImGui.TableSetColumnIndex( 2 );
							ImGui.InputFloat( $"##{waymark.Label}-Y", ref waymark.Y );
							ImGui.TableSetColumnIndex( 3 );
							ImGui.InputFloat( $"##{waymark.Label}-Z", ref waymark.Z );
						}

						ImGui.EndTable();
					}
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Text( "Zone: " );
					if( ImGui.BeginCombo( "##MapID", mConfiguration.GetZoneName( ScratchEditingPreset.MapID ) ) )
					{
						ImGui.Text( "Search: " );
						ImGui.SameLine();
						ImGui.InputText( "##ZoneComboFilter", ref mEditWindowZoneFilterString, 16u );
						if( !EditWindowZoneComboWasOpen )
						{
							ImGui.SetKeyboardFocusHere();
							ImGui.SetItemDefaultFocus();
						}
						foreach( UInt16 zoneID in EditWindowZoneSearcher.GetMatchingZones( mEditWindowZoneFilterString ) )
						{
							if( zoneID != 0 && ImGui.Selectable( mConfiguration.GetZoneName( zoneID ), zoneID == ScratchEditingPreset.MapID ) )
							{
								ScratchEditingPreset.MapID = zoneID;
							}

							//	Uncomment this if we can ever have a better location for the search/filter text box that's not actually in the combo dropdown.
							/*if( zoneID == ScratchEditingPreset.MapID )
							{
								ImGui.SetItemDefaultFocus();
							}*/
						}
						ImGui.EndCombo();
						EditWindowZoneComboWasOpen = true;
					}
					else
					{
						EditWindowZoneComboWasOpen = false;
						mEditWindowZoneFilterString = "";
					}
					ImGui.EndGroup();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					if( ImGui.Button( "Save" ) )
					{
						//*****TODO: Look into why this was even put in a try/catch block.  It doesn't seem like it needs it anymore, if it ever did.*****
						try
						{
							mConfiguration.PresetLibrary.Presets[EditingPresetIndex] = ScratchEditingPreset.GetPreset();
							EditingPresetIndex = -1;
							ScratchEditingPreset = null;
							mConfiguration.Save();
						}
						catch
						{
						}
					}
					ImGui.SameLine();
				}
				else
				{
					ImGui.Text( "Invalid editing data; something went very wrong.  Please press \"Cancel\" and try again." );
				}
				if( ImGui.Button( "Cancel" ) )
				{
					EditingPresetIndex = -1;
					ScratchEditingPreset = null;
				}
				ImGui.SameLine( ImGui.GetWindowWidth() - mButtonMapViewWidth - mRightAlignPadding );
				if( ImGui.Button( "Map View" ) )
				{
					MapWindowVisible = !MapWindowVisible;
				}
			}
			ImGui.End();
		}

		protected void DrawSettingsWindow()
		{
			if( !SettingsWindowVisible )
			{
				return;
			}

			if( ImGui.Begin( "Waymark Settings", ref mSettingsWindowVisible,
				ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse ) )
			{
				ImGui.Checkbox( "Always show preset info pane.", ref mConfiguration.mAlwaysShowInfoPane );
				ImGui.Checkbox( "Clicking the selected preset unselects it.", ref mConfiguration.mAllowUnselectPreset );
				ImGui.Checkbox( "Categorize presets by zone.", ref mConfiguration.mSortPresetsByZone );
				ImGui.Checkbox( "Open and close library with the game's waymark window.", ref mConfiguration.mOpenAndCloseWithFieldMarkerAddon );
				ImGui.Checkbox( "Attach library window to the game's waymark window.", ref mConfiguration.mAttachLibraryToFieldMarkerAddon );
				ImGui.Checkbox( "Show ID numbers next to zone names.", ref mConfiguration.mShowIDNumberNextToZoneNames );
				ImGuiUtils.HelpMarker( "Shows the internal Content Finder ID of the zone/duty in some places.  Generally only used for debugging." );
				ImGui.Checkbox( "Show the index of the preset within the library.", ref mConfiguration.mShowLibraryIndexInPresetList );
				ImGuiUtils.HelpMarker( "The primary use of this is if you need to know the preset index to use within a text command.  You can always leave this disabled if you only use the GUI." );
				/*ImGui.Checkbox( "Allow placement of waymarks client-side in overworld zones.", ref mConfiguration.mAllowClientSidePlacementInOverworldZones );
				ImGuiUtils.HelpMarker( "Lets the plugin attempt to place waymarks in overworld zones that do not function with the game's preset interface.  These will only be visible client-side, and not to other party/alliance members.  This is out of specification behavior for the game, so please read this plugin's readme before enabling." );*/
				ImGui.Checkbox( "Autoload presets from library.", ref mConfiguration.mAutoPopulatePresetsOnEnterInstance );
				ImGuiUtils.HelpMarker( "Automatically loads the first five presets that exist in the library for a zone when you load into it.  THIS WILL OVERWRITE THE GAME'S SLOTS WITHOUT WARNING, so please do not turn this on until you are certain that you have saved any data that you want to keep.  Consider using this with the auto-import option below to reduce the risk of inadvertent preset loss." );
				ImGui.Checkbox( "Autosave presets to library.", ref mConfiguration.mAutoSavePresetsOnInstanceLeave );
				ImGuiUtils.HelpMarker( "Automatically copies any populated game preset slots into the library upon exiting an instance." );
				ImGui.Checkbox( "Suppress responses to text commands (besides \"help\").", ref mConfiguration.mSuppressCommandLineResponses );
				if( ImGui.Button( "Clear All Map View Data" ) )
				{
					ClearAllMapViewStateData();
				}
				ImGuiUtils.HelpMarker( "This deletes all map view pan/zoom/submap state, resetting every map back to default." );
				ImGui.Spacing();
				if( ImGui.Button( "Save and Close" ) )
				{
					mConfiguration.Save();
					SettingsWindowVisible = false;
				}

				ImGui.SameLine( ImGui.GetWindowWidth() - mButtonLibraryWidth - mRightAlignPadding );
				if( ImGui.Button( "Show Library" ) )
				{
					MainWindowVisible = true;
				}
				mButtonLibraryWidth = ImGui.GetItemRectSize().X;
			}
			ImGui.End();
		}

		protected void DrawMapWindow()
		{
			if( !MapWindowVisible )
			{
				return;
			}

			bool showingEditingView = EditingPresetIndex > -1 && ScratchEditingPreset != null;
			if( !showingEditingView )
			{
				CapturedWaymarkIndex = -1;	//	Shouldn't be necessary, but better to be safe than potentially muck up a preset.
			}
			ImGui.SetNextWindowSizeConstraints( new Vector2( 350, 380 ) * ImGui.GetIO().FontGlobalScale, new Vector2( int.MaxValue, int.MaxValue ) );
			if( ImGui.Begin( $"Map View{(showingEditingView ? " - Editing" : "")}###MapViewWindow", ref mMapWindowVisible,
				ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				//	Get TerritoryType ID of map to show, along with the (2D/XZ) zone coordinates of the waymarks.  Do this up front because we can be showing both normal presets or an editing scratch preset in the map view.
				uint territoryTypeIDToShow = 0;
				Vector2[] marker2dCoords = new Vector2[8];
				bool[] markerActiveFlags = new bool[8];
				if( showingEditingView )
				{
					territoryTypeIDToShow = ZoneInfoHandler.GetZoneInfoFromContentFinderID( ScratchEditingPreset.MapID ).TerritoryTypeID;
					for( int i = 0; i < marker2dCoords.Length; ++i )
					{
						marker2dCoords[i] = new Vector2( ScratchEditingPreset.Waymarks[i].X, ScratchEditingPreset.Waymarks[i].Z );
						markerActiveFlags[i] = ScratchEditingPreset.Waymarks[i].Active;
					}
				}
				else if( SelectedPreset > -1 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
				{
					territoryTypeIDToShow = ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID ).TerritoryTypeID;
					marker2dCoords[0] = new Vector2( mConfiguration.PresetLibrary.Presets[SelectedPreset].A.X, mConfiguration.PresetLibrary.Presets[SelectedPreset].A.Z );
					marker2dCoords[1] = new Vector2( mConfiguration.PresetLibrary.Presets[SelectedPreset].B.X, mConfiguration.PresetLibrary.Presets[SelectedPreset].B.Z );
					marker2dCoords[2] = new Vector2( mConfiguration.PresetLibrary.Presets[SelectedPreset].C.X, mConfiguration.PresetLibrary.Presets[SelectedPreset].C.Z );
					marker2dCoords[3] = new Vector2( mConfiguration.PresetLibrary.Presets[SelectedPreset].D.X, mConfiguration.PresetLibrary.Presets[SelectedPreset].D.Z );
					marker2dCoords[4] = new Vector2( mConfiguration.PresetLibrary.Presets[SelectedPreset].One.X, mConfiguration.PresetLibrary.Presets[SelectedPreset].One.Z );
					marker2dCoords[5] = new Vector2( mConfiguration.PresetLibrary.Presets[SelectedPreset].Two.X, mConfiguration.PresetLibrary.Presets[SelectedPreset].Two.Z );
					marker2dCoords[6] = new Vector2( mConfiguration.PresetLibrary.Presets[SelectedPreset].Three.X, mConfiguration.PresetLibrary.Presets[SelectedPreset].Three.Z );
					marker2dCoords[7] = new Vector2( mConfiguration.PresetLibrary.Presets[SelectedPreset].Four.X, mConfiguration.PresetLibrary.Presets[SelectedPreset].Four.Z );

					markerActiveFlags[0] = mConfiguration.PresetLibrary.Presets[SelectedPreset].A.Active;
					markerActiveFlags[1] = mConfiguration.PresetLibrary.Presets[SelectedPreset].B.Active;
					markerActiveFlags[2] = mConfiguration.PresetLibrary.Presets[SelectedPreset].C.Active;
					markerActiveFlags[3] = mConfiguration.PresetLibrary.Presets[SelectedPreset].D.Active;
					markerActiveFlags[4] = mConfiguration.PresetLibrary.Presets[SelectedPreset].One.Active;
					markerActiveFlags[5] = mConfiguration.PresetLibrary.Presets[SelectedPreset].Two.Active;
					markerActiveFlags[6] = mConfiguration.PresetLibrary.Presets[SelectedPreset].Three.Active;
					markerActiveFlags[7] = mConfiguration.PresetLibrary.Presets[SelectedPreset].Four.Active;
				}

				//	Try to draw the maps if we have a valid zone to show.
				if( territoryTypeIDToShow > 0 )
				{
					//	Try to show the map(s); otherwise, show a message that they're still loading.
					if( mMapTextureDictMutex.WaitOne( 0 ) )
					{
						if( MapTextureDict.ContainsKey( (UInt16)territoryTypeIDToShow ) )
						{
							if( MapTextureDict[(UInt16)territoryTypeIDToShow].Count < 1 )
							{
								ImGui.Text( "No maps available for this zone." );
							}
							else
							{
								//	Some things that we'll need as we (attempt to) draw the map.
								var mapList = MapTextureDict[(UInt16)territoryTypeIDToShow];
								var mapInfo = ZoneInfoHandler.GetMapInfoFromTerritoryTypeID( territoryTypeIDToShow );
								string cursorPosText = "X: ---, Y: ---";
								Vector2 windowSize = ImGui.GetWindowSize();
								float mapWidgetSize_Px = Math.Min( windowSize.X - 15 * ImGui.GetIO().FontGlobalScale, windowSize.Y - 63 * ImGui.GetIO().FontGlobalScale );

								//	Ensure that the submap/zoom/pan for this map exists.
								if( !MapViewStateData.ContainsKey( territoryTypeIDToShow ) )
								{
									MapViewStateData.Add( territoryTypeIDToShow, new MapViewState() );
								}
								for( int i = MapViewStateData[territoryTypeIDToShow].SubMapViewData.Count; i < mapInfo.Length; ++i )
								{
									MapViewStateData[territoryTypeIDToShow].SubMapViewData.Add( new MapViewState.SubMapViewState( GetDefaultMapZoom( (float)mapInfo[i].SizeFactor ), new Vector2( 0.5f ) ) );
								}

								//	Aliases
								ref int selectedSubMapIndex = ref MapViewStateData[territoryTypeIDToShow].SelectedSubMapIndex;

								if( selectedSubMapIndex < mapList.Count )
								{
									//	Aliases
									ref float mapZoom = ref MapViewStateData[territoryTypeIDToShow].SubMapViewData[selectedSubMapIndex].Zoom;
									ref Vector2 mapPan = ref MapViewStateData[territoryTypeIDToShow].SubMapViewData[selectedSubMapIndex].Pan;

									ImGui.PushStyleVar( ImGuiStyleVar.WindowPadding, new Vector2( 0 ) );
									ImGui.BeginChild( "##MapImageContainer", new Vector2( mapWidgetSize_Px ), false, ImGuiWindowFlags.NoDecoration );
									Vector2 mapLowerBounds = new Vector2( Math.Min( 1.0f, Math.Max( 0.0f, mapPan.X - mapZoom * 0.5f ) ), Math.Min( 1.0f, Math.Max( 0.0f, mapPan.Y - mapZoom * 0.5f ) ) );
									Vector2 mapUpperBounds = new Vector2( Math.Min( 1.0f, Math.Max( 0.0f, mapPan.X + mapZoom * 0.5f ) ), Math.Min( 1.0f, Math.Max( 0.0f, mapPan.Y + mapZoom * 0.5f ) ) );
									ImGui.ImageButton( mapList[selectedSubMapIndex].ImGuiHandle, new Vector2( mapWidgetSize_Px ), mapLowerBounds, mapUpperBounds, 0, new Vector4( 0, 0, 0, 1 ), new Vector4( 1, 1, 1, 1 ) );
									Vector2 mapWidgetScreenPos = ImGui.GetItemRectMin();
									if( ImGui.IsItemHovered() && CapturedWaymarkIndex < 0 )
									{
										if( ImGui.GetIO().MouseWheel < 0 ) mapZoom *= 1.1f;
										if( ImGui.GetIO().MouseWheel > 0 ) mapZoom *= 0.9f;
									}
									mapZoom = Math.Min( 1.0f, Math.Max( 0.01f, mapZoom ) );
									if( ImGui.IsItemActive() && ImGui.GetIO().MouseDown[0] )
									{
										Vector2 mouseDragDelta = ImGui.GetIO().MouseDelta;
										//	If we have a captured waymark, convert it to screen coordinates, add on the mouse delta, and then convert it back and save off the new location as-appropriate
										if( CapturedWaymarkIndex > -1 && CapturedWaymarkIndex < marker2dCoords.Length )
										{
											Vector2 capturedMarkerPixelCoords = MapTextureCoordsToScreenCoords(	mapInfo[selectedSubMapIndex].GetPixelCoordinates( marker2dCoords[CapturedWaymarkIndex] ),
																												mapLowerBounds,
																												mapUpperBounds,
																												new Vector2( mapWidgetSize_Px ),
																												mapWidgetScreenPos );

											capturedMarkerPixelCoords += mouseDragDelta;

											Vector2 capturedMarkerTexCoords = MapScreenCoordsToMapTextureCoords(	capturedMarkerPixelCoords,
																													mapLowerBounds,
																													mapUpperBounds,
																													new Vector2( mapWidgetSize_Px ),
																													mapWidgetScreenPos );

											marker2dCoords[CapturedWaymarkIndex] = mapInfo[selectedSubMapIndex].GetMapCoordinates( capturedMarkerTexCoords );

											if( EditingPresetIndex > -1 && ScratchEditingPreset != null )
											{
												ScratchEditingPreset.Waymarks[CapturedWaymarkIndex].X = marker2dCoords[CapturedWaymarkIndex].X;
												ScratchEditingPreset.Waymarks[CapturedWaymarkIndex].Z = marker2dCoords[CapturedWaymarkIndex].Y;
											}
										}
										//	Otherwise, we're just panning the map.
										else
										{
											mapPan.X -= mouseDragDelta.X * mapZoom / mapWidgetSize_Px;
											mapPan.Y -= mouseDragDelta.Y * mapZoom / mapWidgetSize_Px;
										}
									}
									else
									{
										CapturedWaymarkIndex = -1;
									}
									mapPan.X = Math.Min( 1.0f - mapZoom * 0.5f, Math.Max( 0.0f + mapZoom * 0.5f, mapPan.X ) );
									mapPan.Y = Math.Min( 1.0f - mapZoom * 0.5f, Math.Max( 0.0f + mapZoom * 0.5f, mapPan.Y ) );

									if( ImGui.IsItemHovered() )
									{
										Vector2 mapPixelCoords = ImGui.GetMousePos() - mapWidgetScreenPos;
										//	If we are dragging a marker, offset the mouse position in here to show the actual point location, not the mouse position).
										if( showingEditingView && CapturedWaymarkIndex > -1 )
										{
											mapPixelCoords += CapturedWaymarkOffset;
										}
										Vector2 mapNormCoords = mapPixelCoords / mapWidgetSize_Px * ( mapUpperBounds - mapLowerBounds ) + mapLowerBounds;
										Vector2 mapRealCoords = mapInfo[selectedSubMapIndex].GetMapCoordinates( mapNormCoords * 2048.0f );
										cursorPosText = $"X: {mapRealCoords.X:0.00}, Y: {mapRealCoords.Y:0.00}";
									}
									for( int i = 0; i < 8; ++i )
									{
										if( markerActiveFlags[i] )
										{
											Vector2 waymarkMapPt = MapTextureCoordsToScreenCoords(	mapInfo[selectedSubMapIndex].GetPixelCoordinates( marker2dCoords[i] ),
																									mapLowerBounds,
																									mapUpperBounds,
																									new Vector2( mapWidgetSize_Px ),
																									mapWidgetScreenPos );

											ImGui.GetWindowDrawList().AddImage( mWaymarkIconTextures[i].ImGuiHandle, waymarkMapPt - mWaymarkMapIconHalfSize_Px, waymarkMapPt + mWaymarkMapIconHalfSize_Px );

											//	Capture the waymark if appropriate.
											if( showingEditingView &&
												CapturedWaymarkIndex < 0 &&
												ImGui.GetIO().MouseClicked[0] &&
												ImGui.GetIO().MousePos.X >= mapWidgetScreenPos.X &&
												ImGui.GetIO().MousePos.X <= mapWidgetScreenPos.X + mapWidgetSize_Px &&
												ImGui.GetIO().MousePos.Y >= mapWidgetScreenPos.Y &&
												ImGui.GetIO().MousePos.Y <= mapWidgetScreenPos.Y + mapWidgetSize_Px &&
												ImGui.GetIO().MousePos.X >= waymarkMapPt.X - mWaymarkMapIconHalfSize_Px.X &&
												ImGui.GetIO().MousePos.X <= waymarkMapPt.X + mWaymarkMapIconHalfSize_Px.X &&
												ImGui.GetIO().MousePos.Y >= waymarkMapPt.Y - mWaymarkMapIconHalfSize_Px.Y &&
												ImGui.GetIO().MousePos.Y <= waymarkMapPt.Y + mWaymarkMapIconHalfSize_Px.Y )
											{
												CapturedWaymarkIndex = i;
												CapturedWaymarkOffset = waymarkMapPt - ImGui.GetIO().MousePos;
											}
										}
									}
									ImGui.EndChild();
									ImGui.PopStyleVar();
									ImGuiUtils.HelpMarker(
											"A Note on Coordinate Systems: The game internally uses a right-handed 3D coordinate system, " +
											"with X running West to East, Y running down to up, and Z running North to South.  The on-map " +
											"coordinate system is a 2D projection of the XZ plane, with X running West to East, and Y running " +
											"North to South.  Please note that the coordinates presented in chat links or on the map widgets " +
											"in game are scaled to arbitrary values, and the Y and Z axes are swapped.  This plugin uses the " +
											"game's internal coordinate systems.\r\n\r\ntl;dr: Y is up/down for 3D, and North/South for 2D.",
											false );
									ImGui.SameLine();
									ImGui.Text( cursorPosText );
								}

								if( mapList.Count <= 1 || selectedSubMapIndex >= mapList.Count )
								{
									selectedSubMapIndex = 0;
								}
								else
								{
									float subMapComboWidth = 0.0f;
									List<string> subMaps = new List<string>();
									for( int i = 0; i < mapInfo.Length; ++i )
									{
										string subMapName = mapInfo[i].PlaceNameSub.Trim().Length < 1 ? $"Unnamed Sub-map {i + 1}" : mapInfo[i].PlaceNameSub;
										subMaps.Add( subMapName );
										subMapComboWidth = Math.Max( subMapComboWidth, ImGui.CalcTextSize( subMapName ).X );
									}
									subMapComboWidth += 40.0f;

									ImGui.SameLine( Math.Max( mapWidgetSize_Px /*- ImGui.CalcTextSize( cursorPosText ).X*/ - subMapComboWidth + 8, 0 ) );
									ImGui.SetNextItemWidth( subMapComboWidth );
									ImGui.Combo( "", ref selectedSubMapIndex, subMaps.ToArray(), mapList.Count );
								}
							}
						}
						else
						{
							ImGui.Text( "Loading zone map(s)." );
							LoadMapTextures( (UInt16)territoryTypeIDToShow );
						}

						mMapTextureDictMutex.ReleaseMutex();
					}
					else
					{
						ImGui.Text( "Loading zone map(s)." );
					}
				}
				else
				{
					ImGui.Text( "No Preset Selected" );
				}
			}

			ImGui.End();
		}

		protected Vector2 MapTextureCoordsToScreenCoords( Vector2 mapTextureCoords_Px, Vector2 mapVisibleLowerBounds_Norm, Vector2 mapVisibleUpperBounds_Norm, Vector2 mapViewportSize_Px, Vector2 mapViewportScreenPos_Px )
		{
			Vector2 newScreenCoords = mapTextureCoords_Px;
			newScreenCoords /= 2048.0f;
			newScreenCoords = ( newScreenCoords - mapVisibleLowerBounds_Norm ) / ( mapVisibleUpperBounds_Norm - mapVisibleLowerBounds_Norm ) * mapViewportSize_Px;
			newScreenCoords = newScreenCoords + mapViewportScreenPos_Px;

			return newScreenCoords;
		}

		protected Vector2 MapScreenCoordsToMapTextureCoords( Vector2 mapScreenCoords_Px, Vector2 mapVisibleLowerBounds_Norm, Vector2 mapVisibleUpperBounds_Norm, Vector2 mapViewportSize_Px, Vector2 mapViewportScreenPos_Px )
		{
			Vector2 newMapTexCoords = mapScreenCoords_Px;
			newMapTexCoords -= mapViewportScreenPos_Px;
			newMapTexCoords /= mapViewportSize_Px;
			newMapTexCoords *= mapVisibleUpperBounds_Norm - mapVisibleLowerBounds_Norm;
			newMapTexCoords += mapVisibleLowerBounds_Norm;
			newMapTexCoords *= 2048.0f;
			return newMapTexCoords;
		}

		protected void CopyPresetToGameSlot( WaymarkPreset preset, uint slot )
		{
			if( ZoneInfoHandler.IsKnownContentFinderID( preset.MapID ) && slot >= 1 && slot <= MemoryHandler.MaxPresetSlotNum )
			{
				try
				{
					MemoryHandler.WriteSlot( slot, preset.GetAsGamePreset() );
				}
				catch( Exception e )
				{
					PluginLog.Log( $"Error while copying preset data to game slot: {e}" );
				}
			}
		}

		public void SetCurrentTerritoryTypeID( UInt16 ID )
		{
			CurrentTerritoryTypeID = ID;
		}

		protected void LoadMapTextures( UInt16 territoryTypeID )
		{
			//	Only add/load stuff that we don't already have.  Callers should be checking this, but we should too.
			if( !MapTextureDict.ContainsKey( territoryTypeID ) )
			{

				Task.Run( () =>
				{
					//	Add the entry.
					MapTextureDict.Add( territoryTypeID, new List<TextureWrap>() );

					//	Grab the texture files for this zone's maps and load them in.
					foreach( var map in ZoneInfoHandler.GetMapInfoFromTerritoryTypeID( territoryTypeID ) )
					{
						//	Lock the mutex.  If we can't get it within ten seconds, just give up.
						if( mMapTextureDictMutex.WaitOne( 30000 ) )
						{
							//	Get, process, and add the map texture.
							try
							{
								//	TODO: Check for the *m file and/or use the discovery flags to determine whether we need to composite the textures.*****
								var texFile = mDataManager.GetFile<Lumina.Data.Files.TexFile>( map.GetMapFilePath() );
								var parchmentTexFile = mDataManager.GetFile<Lumina.Data.Files.TexFile>( map.GetMapParchmentImageFilePath() );
								if( texFile != null )
								{
									byte[] texData = MapTextureBlend( texFile.GetRgbaImageData(), parchmentTexFile == null ? null : parchmentTexFile.GetRgbaImageData() );

									var tex = mPluginInterface.UiBuilder.LoadImageRaw( texData, texFile.Header.Width, texFile.Header.Height, 4 );
									if( tex != null && tex.ImGuiHandle != IntPtr.Zero )
									{
										MapTextureDict[territoryTypeID].Add( tex );
									}
								}
							}
							catch
							{
							}

							//	Release the mutex.
							mMapTextureDictMutex.ReleaseMutex();
						}
					}
				} );
			}
		}

		protected byte[] MapTextureBlend( byte[] mapTex, byte[] parchmentTex = null )
		{
			if( parchmentTex == null || parchmentTex.Length != mapTex.Length )
			{
				return mapTex;
			}
			else
			{
				byte[] blendedTex = new byte[mapTex.Length];
				for( int i = 0; i < blendedTex.Length; ++i )
				{
					//	A simple multiply probably gets us close enough for now.
					blendedTex[i] = (byte)( (float)mapTex[i] * (float)parchmentTex[i] / 255f );
				}

				return blendedTex;
			}
		}

		protected float GetDefaultMapZoom( float mapScaleFactor )
		{
			//	Lookup Table
			float[] xValues = { 100, 200, 400, 800 };
			float[] yValues = { 1.0f, 0.7f, 0.2f, 0.1f};

			//	Do the interpolation.
			if( mapScaleFactor < xValues[0] )
			{
				return yValues[0];
			}
			else if( mapScaleFactor > xValues[xValues.Length - 1] )
			{
				return yValues[xValues.Length - 1];
			}
			else
			{
				for( int i = 0; i < xValues.Length - 1; ++i )
				{
					if( mapScaleFactor > xValues[i + 1] ) continue;
					return ( mapScaleFactor - xValues[i] ) / ( xValues[i + 1] - xValues[i] ) * ( yValues[i + 1] - yValues[i] ) + yValues[i];
				}

				return 1.0f;
			}
		}

		protected void ClearAllMapViewStateData()
		{
			MapViewStateData.Clear();
			string viewStateDataFilePath = Path.Join( mPluginInterface.GetPluginConfigDirectory(), $"\\MapViewStateData_v1.json" );
			if( File.Exists( viewStateDataFilePath ) )
			{
				File.Delete( viewStateDataFilePath );
			}
		}

		public void ShowGimpedModeWarningWindow( bool force = false )
		{
			if( !HaveShownGimpedModeWarningMessage || force )
			{
				GimpedModeWarningWindowVisible = true;
			}
		}

		protected Configuration mConfiguration;
		protected DalamudPluginInterface mPluginInterface;
		protected DataManager mDataManager;
		protected CommandManager mCommandManager;
		protected GameGui mGameGui;

		//	Need a real backing field on the following properties for use with ImGui.
		protected bool mMainWindowVisible = false;
		public bool MainWindowVisible
		{
			get { return mMainWindowVisible; }
			set { mMainWindowVisible = value; }
		}

		protected bool mSettingsWindowVisible = false;
		public bool SettingsWindowVisible
		{
			get { return mSettingsWindowVisible; }
			set { mSettingsWindowVisible = value; }
		}

		protected bool mMapWindowVisible = false;
		public bool MapWindowVisible
		{
			get { return mMapWindowVisible; }
			set { mMapWindowVisible = value; }
		}

		protected bool mGimpedModeWarningWindowVisible = false;
		public bool GimpedModeWarningWindowVisible
		{
			get { return mGimpedModeWarningWindowVisible; }
			set { mGimpedModeWarningWindowVisible = value; }
		}
		protected bool HaveShownGimpedModeWarningMessage = false;

		protected string mPresetImportString = "";
		public string PresetImportString
		{
			get { return mPresetImportString; }
			set { mPresetImportString = value; }
		}

		public Vector2 MainWindowPos { get; protected set; }
		public Vector2 MainWindowSize { get; protected set; }

		public int SelectedPreset { get; protected set; } = -1;
		public bool WantToDeleteSelectedPreset { get; protected set; } = false;
		public int EditingPresetIndex { get; protected set; } = -1;
		public bool EditingPreset => EditingPresetIndex != -1;
		protected  ScratchPreset ScratchEditingPreset { get; set; }
		protected UInt16 CurrentTerritoryTypeID { get; set; }
		protected ZoneSearcher EditWindowZoneSearcher { get; set; } = new ZoneSearcher();
		protected string mEditWindowZoneFilterString = "";
		protected bool EditWindowZoneComboWasOpen { get; set; } = false;
		protected bool FieldMarkerAddonWasOpen { get; set; } = false;
		
		//	Padding and storing width to right-align buttons.  Initialization value probably doesn't much matter for the saved button widths, since they'll be updated after the first rendered frame.
		private float mRightAlignPadding = 15;
		private float mButtonMapViewWidth = 79;
		private float mButtonLibraryWidth = 90;

		protected Dictionary<UInt16, List<TextureWrap>> MapTextureDict { get; set; } = new Dictionary<UInt16, List<TextureWrap>>();
		protected Mutex mMapTextureDictMutex = new Mutex();
		protected readonly TextureWrap[] mWaymarkIconTextures = new TextureWrap[8];
		protected int CapturedWaymarkIndex { get; set; } = -1;
		protected Vector2 CapturedWaymarkOffset { get; set; } = new Vector2( 0, 0 );
		protected static readonly Vector2 mWaymarkMapIconHalfSize_Px = new Vector2( 15, 15 );

		protected readonly IntPtr mpLibraryPresetDragAndDropData;
		protected readonly IntPtr mpEditWaymarkDragAndDropData;

		protected Dictionary<uint, MapViewState> MapViewStateData { get; set; } = new Dictionary<uint, MapViewState>();
	}

	//	Helper class to store data on how each zones map is being viewed.  Using plain fields instead of properties so that we can easily hold short-term refs to them for aliasing.
	public class MapViewState
	{
		public class SubMapViewState
		{
			public SubMapViewState( float zoom, Vector2 pan )
			{
				Zoom = zoom;
				Pan = pan;
			}

			//	We want to be able to get these as refs, so no properties.
			public float Zoom;
			public Vector2 Pan;
		}

		public int SelectedSubMapIndex = 0;
		public List<SubMapViewState> SubMapViewData { get; protected set; } = new List<SubMapViewState>();
	}

	//	We need this because we can't pass the properties from the regular Waymark class as refs to ImGui stuff.  It's an absolute dog's breakfast, but whatever at this point honestly.
	public class ScratchPreset
	{
		public class ScratchWaymark
		{
			public float X;
			public float Y;
			public float Z;
			public int ID;
			public bool Active;
			public string Label;
		}

		public void SwapWaymarks( int index1, int index2 )
		{
			if( index1 == index2 ||
				index1 < 0 ||
				index2 < 0 ||
				index1 >= Waymarks.Count ||
				index2 >= Waymarks.Count )
			return;

			bool tempActive = Waymarks[index1].Active;
			float tempX = Waymarks[index1].X;
			float tempY = Waymarks[index1].Y;
			float tempZ = Waymarks[index1].Z;

			Waymarks[index1].Active = Waymarks[index2].Active;
			Waymarks[index1].X = Waymarks[index2].X;
			Waymarks[index1].Y = Waymarks[index2].Y;
			Waymarks[index1].Z = Waymarks[index2].Z;

			Waymarks[index2].Active = tempActive;
			Waymarks[index2].X = tempX;
			Waymarks[index2].Y = tempY;
			Waymarks[index2].Z = tempZ;
		}

		public ScratchPreset( WaymarkPreset preset )
		{
			Name = preset.Name;
			MapID = preset.MapID;
			Waymarks = new List<ScratchWaymark>();

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.A.X;
			Waymarks.Last().Y = preset.A.Y;
			Waymarks.Last().Z = preset.A.Z;
			Waymarks.Last().ID = preset.A.ID;
			Waymarks.Last().Active = preset.A.Active;
			Waymarks.Last().Label = "A";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.B.X;
			Waymarks.Last().Y = preset.B.Y;
			Waymarks.Last().Z = preset.B.Z;
			Waymarks.Last().ID = preset.B.ID;
			Waymarks.Last().Active = preset.B.Active;
			Waymarks.Last().Label = "B";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.C.X;
			Waymarks.Last().Y = preset.C.Y;
			Waymarks.Last().Z = preset.C.Z;
			Waymarks.Last().ID = preset.C.ID;
			Waymarks.Last().Active = preset.C.Active;
			Waymarks.Last().Label = "C";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.D.X;
			Waymarks.Last().Y = preset.D.Y;
			Waymarks.Last().Z = preset.D.Z;
			Waymarks.Last().ID = preset.D.ID;
			Waymarks.Last().Active = preset.D.Active;
			Waymarks.Last().Label = "D";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.One.X;
			Waymarks.Last().Y = preset.One.Y;
			Waymarks.Last().Z = preset.One.Z;
			Waymarks.Last().ID = preset.One.ID;
			Waymarks.Last().Active = preset.One.Active;
			Waymarks.Last().Label = "1";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.Two.X;
			Waymarks.Last().Y = preset.Two.Y;
			Waymarks.Last().Z = preset.Two.Z;
			Waymarks.Last().ID = preset.Two.ID;
			Waymarks.Last().Active = preset.Two.Active;
			Waymarks.Last().Label = "2";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.Three.X;
			Waymarks.Last().Y = preset.Three.Y;
			Waymarks.Last().Z = preset.Three.Z;
			Waymarks.Last().ID = preset.Three.ID;
			Waymarks.Last().Active = preset.Three.Active;
			Waymarks.Last().Label = "3";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.Four.X;
			Waymarks.Last().Y = preset.Four.Y;
			Waymarks.Last().Z = preset.Four.Z;
			Waymarks.Last().ID = preset.Four.ID;
			Waymarks.Last().Active = preset.Four.Active;
			Waymarks.Last().Label = "4";
		}

		public WaymarkPreset GetPreset()
		{
			WaymarkPreset newPreset = new WaymarkPreset();

			newPreset.Name = Name;
			newPreset.MapID = MapID;

			newPreset.A.X = Waymarks[0].X;
			newPreset.A.Y = Waymarks[0].Y;
			newPreset.A.Z = Waymarks[0].Z;
			newPreset.A.ID = Waymarks[0].ID;
			newPreset.A.Active = Waymarks[0].Active;

			newPreset.B.X = Waymarks[1].X;
			newPreset.B.Y = Waymarks[1].Y;
			newPreset.B.Z = Waymarks[1].Z;
			newPreset.B.ID = Waymarks[1].ID;
			newPreset.B.Active = Waymarks[1].Active;

			newPreset.C.X = Waymarks[2].X;
			newPreset.C.Y = Waymarks[2].Y;
			newPreset.C.Z = Waymarks[2].Z;
			newPreset.C.ID = Waymarks[2].ID;
			newPreset.C.Active = Waymarks[2].Active;

			newPreset.D.X = Waymarks[3].X;
			newPreset.D.Y = Waymarks[3].Y;
			newPreset.D.Z = Waymarks[3].Z;
			newPreset.D.ID = Waymarks[3].ID;
			newPreset.D.Active = Waymarks[3].Active;

			newPreset.One.X = Waymarks[4].X;
			newPreset.One.Y = Waymarks[4].Y;
			newPreset.One.Z = Waymarks[4].Z;
			newPreset.One.ID = Waymarks[4].ID;
			newPreset.One.Active = Waymarks[4].Active;

			newPreset.Two.X = Waymarks[5].X;
			newPreset.Two.Y = Waymarks[5].Y;
			newPreset.Two.Z = Waymarks[5].Z;
			newPreset.Two.ID = Waymarks[5].ID;
			newPreset.Two.Active = Waymarks[5].Active;

			newPreset.Three.X = Waymarks[6].X;
			newPreset.Three.Y = Waymarks[6].Y;
			newPreset.Three.Z = Waymarks[6].Z;
			newPreset.Three.ID = Waymarks[6].ID;
			newPreset.Three.Active = Waymarks[6].Active;

			newPreset.Four.X = Waymarks[7].X;
			newPreset.Four.Y = Waymarks[7].Y;
			newPreset.Four.Z = Waymarks[7].Z;
			newPreset.Four.ID = Waymarks[7].ID;
			newPreset.Four.Active = Waymarks[7].Active;

			return newPreset;
		}

		public string Name = "";
		public UInt16 MapID = 0;
		public List<ScratchWaymark> Waymarks;
	}
}
