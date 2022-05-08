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
using CheapLoc;

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
			mpEditWaymarkCoordDragAndDropData = Marshal.AllocHGlobal( Marshal.SizeOf<Vector3>() );
			if( mpLibraryPresetDragAndDropData == IntPtr.Zero ||
				mpEditWaymarkDragAndDropData == IntPtr.Zero ||
				mpEditWaymarkCoordDragAndDropData  == IntPtr.Zero )
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

			//	Clean up any other textures.
			mCoordinateSystemsDiagram?.Dispose();
			for( int i = 0; i < mWaymarkIconTextures.Length; ++i )
			{
				mWaymarkIconTextures[i]?.Dispose();
			}

			//	Free the drag and drop data.
			Marshal.FreeHGlobal( mpLibraryPresetDragAndDropData );
			Marshal.FreeHGlobal( mpEditWaymarkDragAndDropData );
			Marshal.FreeHGlobal( mpEditWaymarkCoordDragAndDropData );
		}

		public void Initialize()
		{
			mCoordinateSystemsDiagram = mPluginInterface.UiBuilder.LoadImage( Path.Join( mPluginInterface.AssemblyLocation.DirectoryName, "Resources\\CoordinateSystemDiagrams.png" ) );
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
			DrawHelpWindow();
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
			if( ImGui.Begin( Loc.Localize( "Window Title: Waymark Library", "Waymark Library" ) + "###Waymark Library", ref mMainWindowVisible, ImGuiWindowFlags.NoCollapse ) )
			{
				ImGuiUtils.TitleBarHelpButton( () => { ShowHelpWindow( HelpWindowPage.General ); }, 1, UiBuilder.IconFont );

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
				ImGui.Checkbox( Loc.Localize( "Config Option: Filter on Current Zone", "Filter on Current Zone" ) + "###Filter on Current Zone Checkbox", ref mConfiguration.mFilterOnCurrentZone );
				if( mConfiguration.FilterOnCurrentZone != previouslyFilteredOnZone ) mConfiguration.Save(); //	I'd rather just save the state when the plugin is unloaded, but that's not been feasible in the past.
				string saveCurrentWaymarksButtonText = Loc.Localize( "Button: Save Current Waymarks", "Save Current Waymarks" );
				ImGui.SameLine( ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize( saveCurrentWaymarksButtonText ).X - ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().WindowPadding.X );
				if( MemoryHandler.FoundDirectSaveSigs() )
				{
					if( ImGui.Button( saveCurrentWaymarksButtonText + "###Save Current Waymarks Button" ) )
					{
						GamePreset currentWaymarks = new();
						if( MemoryHandler.GetCurrentWaymarksAsPresetData( ref currentWaymarks ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( currentWaymarks ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
					}
				}
				else
				{
					ImGui.PushStyleColor( ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] * 0.5f );
					ImGui.PushStyleColor( ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] );
					ImGui.PushStyleColor( ImGuiCol.ButtonActive, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] );
					ImGui.PushStyleColor( ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled] );

					ImGui.Button( saveCurrentWaymarksButtonText + "###Save Current Waymarks Button" );

					ImGui.PopStyleColor();
					ImGui.PopStyleColor();
					ImGui.PopStyleColor();
					ImGui.PopStyleColor();
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
											ImGui.Text( Loc.Localize( "Drag and Drop Preview: Moving Preset", "Moving: " ) + $"{mConfiguration.PresetLibrary.Presets[indices[i]].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + indices[i].ToString() + ")" : "" )}" );
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
												ImGui.Selectable( Loc.Localize( "Drag and Drop Preview: Move to Bottom", "<Move To Bottom>" ) + "###<Move To Bottom>" );
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
						if( ImGui.CollapsingHeader( Loc.Localize( "Header: Presets", "Presets" ) + "###Presets" ) )
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
										ImGui.Text( Loc.Localize( "Drag and Drop Preview: Moving Preset", "Moving: " ) + $"{mConfiguration.PresetLibrary.Presets[i].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + i.ToString() + ")" : "" )}" );
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
										ImGui.Selectable( Loc.Localize( "Drag and Drop Preview: Move to Bottom", "<Move To Bottom>" ) + "###<Move To Bottom>" );
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
					ImGui.Text( Loc.Localize( "Main Window Text: Library Empty", "Preset library empty!" ) );
				}
				ImGui.EndGroup();

				//	Input box for import.
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				if( ImGui.CollapsingHeader( Loc.Localize( "Header: Import Options", "Import Options" ) + "###Import Options" ) )
				{
					ImGui.BeginGroup(); //Buttons don't seem to work under a header without being in a group.
					ImGui.InputTextWithHint( "##JSONImportTextBox", "Paste a preset here and click \"Import\".", ref mPresetImportString, 1024 );	//Most exports max out around 500 characters with all waymarks, so this leaves heaps of room for a long name.
					ImGui.SameLine();
					if( ImGui.Button( Loc.Localize( "Button: Import", "Import" ) + "###Import Button" ) )
					{
						PluginLog.LogInformation( $"Attempting to import preset string: \"{mPresetImportString}\"" );
						if( mConfiguration.PresetLibrary.ImportPreset( PresetImportString ) >= 0 )
						{
							PresetImportString = "";
							mConfiguration.Save();
						}
					}
					if( MemoryHandler.FoundSavedPresetSigs() )
					{
						//ImGui.SameLine();
						ImGui.Text( Loc.Localize( "Main Window Text: Import from Game Slot Label", "Or import from game slot: " ) );
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
						ImGuiUtils.URLLink( "https://github.com/PunishedPineapple/WaymarkPresetPlugin/wiki/Preset-Resources", Loc.Localize( "Main Window Text: Preset Resources Link", "Where to find importable presets" ), false, UiBuilder.IconFont );
					}
					catch( Exception e )
					{
						PluginLog.LogWarning( $"Unable to open the requested link:\r\n{e}" );
					}
					ImGui.EndGroup();
				}
				if( ImGui.CollapsingHeader( Loc.Localize( "Header: Export and Backup Options", "Export/Backup Options" ) + "###Export/Backup Options" ) )
				{
					ImGui.BeginGroup(); //Buttons don't seem to work under a header without being in a group.
					if( ImGui.Button( Loc.Localize( "Button: Export All Presets to Clipboard", "Export All Presets to Clipboard" ) + "###Export All Presets to Clipboard Button" ) )
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
					if( ImGui.Button( Loc.Localize( "Button: Backup Current Config", "Backup Current Config" ) + "###Backup Current Config Button" ) )
					{
						mConfiguration.BackupConfigFile();
					}
					ImGuiUtils.HelpMarker( Loc.Localize( "Help: Backup Current Config", "Copies the current config file to a backup folder in the Dalamud \"pluginConfigs\" directory." ) );
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

			DrawInfoWindowLayoutPass();

			ImGui.SetNextWindowSize( mInfoWindowSize );
			ImGui.SetNextWindowPos( new Vector2( MainWindowPos.X + MainWindowSize.X, MainWindowPos.Y ) );	//Note that this does *not* need to be viewport-relative, since it is just an offset relative to the library window.
			if( ImGui.Begin( Loc.Localize( "Window Title: Preset Info", "Preset Info" ) + "###Preset Info", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar ) )
			{
				if( SelectedPreset >= 0 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
				{
					ImGui.BeginGroup();
					ImGui.Text( Loc.Localize( "Info Pane Text: Copy to Slot Label", "Copy to slot:" ) );
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
					float rightAlignPos;
					string placeButtonText = Loc.Localize( "Button: Place", "Place" );
					ImGui.SameLine();
					if( MemoryHandler.FoundDirectPlacementSigs() )
					{
						if( ImGui.Button( placeButtonText + "###Place" ) )
						{
							MemoryHandler.PlacePreset( mConfiguration.PresetLibrary.Presets[SelectedPreset].GetAsGamePreset() /*, mConfiguration.AllowClientSidePlacementInOverworldZones*/ );
						}
					}
					else
					{
						ImGui.PushStyleColor( ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] * 0.5f );
						ImGui.PushStyleColor( ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] );
						ImGui.PushStyleColor( ImGuiCol.ButtonActive, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] );
						ImGui.PushStyleColor( ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled] );
						
						ImGui.Button( placeButtonText + "###Place" );

						ImGui.PopStyleColor();
						ImGui.PopStyleColor();
						ImGui.PopStyleColor();
						ImGui.PopStyleColor();
					}
					mInfoWindowSize.X = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X;
					rightAlignPos = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;


					ImGui.EndGroup();
					ImGui.Text( Loc.Localize( "Info Pane Text: Preset Info Label", "Preset Info:" ) );
					string mapViewButtonText = Loc.Localize( "Button: Map View", "Map View" );
					ImGui.SameLine( rightAlignPos - ImGui.CalcTextSize( "mapViewButtonText" ).X - ImGui.GetStyle().WindowPadding.X - ImGui.GetStyle().FramePadding.X * 2 );
					if( ImGui.Button( mapViewButtonText + "###Map View Button" ) )
					{
						MapWindowVisible = !MapWindowVisible;
					}

					if( ImGui.BeginTable( "###PresetInfoPaneWaymarkDataTable", 4 ) )
					{
						ImGui.TableSetupColumn( Loc.Localize( "Info Pane Text: Waymark Column Header", "Waymark" ) + "###Waymark", ImGuiTableColumnFlags.WidthFixed, 15 * ImGui.GetIO().FontGlobalScale );
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
							ImGuiUtils.RightAlignTableText( waymark.Active ? waymark.X.ToString( "0.00" ) : Loc.Localize( "Info Pane Text: Unused Waymark", "Unused" ) );
							ImGui.TableSetColumnIndex( 2 );
							ImGuiUtils.RightAlignTableText( waymark.Active ? waymark.Y.ToString( "0.00" ) : " " );
							ImGui.TableSetColumnIndex( 3 );
							ImGuiUtils.RightAlignTableText( waymark.Active ? waymark.Z.ToString( "0.00" ) : " " );
						}
						ImGui.EndTable();
					}

					string zoneStr = ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID ).DutyName;
					zoneStr += mConfiguration.ShowIDNumberNextToZoneNames ? $" ({mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID})" : "";
					ImGui.Text( String.Format( Loc.Localize( "Info Pane Text: Zone Label", "Zone: {}"), zoneStr ) );
					ImGui.Text( String.Format( Loc.Localize( "Info Pane Text: Last Modified Label", "Last Modified: {0}"), mConfiguration.PresetLibrary.Presets[SelectedPreset].Time.LocalDateTime ) );

					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					if( ImGui.Button( Loc.Localize( "Button: Export to Clipboard", "Export to Clipboard" ) + "###Export to Clipboard" ) )
					{
						if( SelectedPreset >= 0 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
						{
							Win32Clipboard.CopyTextToClipboard( WaymarkPresetExport.GetExportString( mConfiguration.PresetLibrary.Presets[SelectedPreset] ) );
						}
					}
					ImGui.SameLine();
					if( ImGui.Button( Loc.Localize( "Button: Edit", "Edit" ) + "###Edit" ) && EditingPresetIndex == -1 )  //Don't want to let people start editing while the edit window is already open.
					{
						EditingPresetIndex = SelectedPreset;
						ScratchEditingPreset = new ScratchPreset( mConfiguration.PresetLibrary.Presets[EditingPresetIndex] );
					}
					ImGui.SameLine();
					ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
					if( ImGui.Button( Loc.Localize( "Button: Delete", "Delete" ) + "###Delete" ) && EditingPresetIndex == -1 )
					{
						WantToDeleteSelectedPreset = true;
					}
					mInfoWindowSize.X = Math.Max( mInfoWindowSize.X, ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X );
					if( WantToDeleteSelectedPreset )
					{
						ImGui.Text( Loc.Localize( "Info Pane Text: Confirm Delete Label", "Confirm delete: " ) );
						ImGui.SameLine();
						if( ImGui.Button( Loc.Localize( "Button: Yes", "Yes" ) + "###Yes Button" ) )
						{
							mConfiguration.PresetLibrary.DeletePreset( SelectedPreset );
							WantToDeleteSelectedPreset = false;
							if( SelectedPreset == EditingPresetIndex ) EditingPresetIndex = -1;
							SelectedPreset = -1;
							mConfiguration.Save();
						}
						ImGui.PushStyleColor( ImGuiCol.Text, 0xffffffff );
						ImGui.SameLine();
						if( ImGui.Button( Loc.Localize( "Button: No", "No" ) + "###No Button" ) )
						{
							WantToDeleteSelectedPreset = false;
						}
						ImGui.PopStyleColor();
					}
					ImGui.PopStyleColor();
					mInfoWindowSize.Y = ImGui.GetItemRectMax().Y - ImGui.GetWindowPos().Y + ImGui.GetStyle().WindowPadding.Y;
				}
				else
				{
					ImGui.Text( Loc.Localize( "Info Pane Text: No Preset Selected", "No preset selected." ) );
				}
			}

			ImGui.End();
		}

		protected void DrawInfoWindowLayoutPass()
		{
			//	Actually zero alpha culls the window.
			ImGui.PushStyleVar( ImGuiStyleVar.Alpha, float.Epsilon );

			ImGui.SetNextWindowSize( new( 100f ) );
			ImGui.SetNextWindowPos( ImGuiHelpers.MainViewport.Pos + Vector2.One );
			if( ImGui.Begin( "Preset Info (Layout Pass)", ImGuiUtils.LayoutWindowFlags ) )
			{
				ImGui.Text( Loc.Localize( "Info Pane Text: Copy to Slot Label", "Copy to slot:" ) );
				for( int i = 1; i <= MemoryHandler.MaxPresetSlotNum; ++i )
				{
					ImGui.SameLine();
					ImGui.Button( $"{i}" );
				}
				ImGui.SameLine();
				ImGui.Button( Loc.Localize( "Button: Place", "Place" ) + "###Place" );
				mInfoWindowSize.X = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X;
				ImGui.Button( "My Width Doesn't Matter" );

				if( ImGui.BeginTable( "Real Fake Tables", 1 ) )
				{
					ImGui.TableSetupColumn( "Hey I'm Fake", ImGuiTableColumnFlags.WidthFixed, 15 * ImGui.GetIO().FontGlobalScale );

					for( int i = 0; i < 8; ++i )
					{
						ImGui.TableNextRow();
						ImGui.TableSetColumnIndex( 0 );
						ImGui.Text( $"Nothing to see here." );
					}
					ImGui.EndTable();
				}

				ImGui.Text( $"Who cares!" );
				ImGui.Text( $"Not me!" );

				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();

				ImGui.Button( Loc.Localize( "Button: Export to Clipboard", "Export to Clipboard" ) + "###Export to Clipboard" );
				ImGui.SameLine();
				ImGui.Button( Loc.Localize( "Button: Edit", "Edit" ) + "###Edit" );
				ImGui.SameLine();
				ImGui.Button( Loc.Localize( "Button: Delete", "Delete" ) + "###Delete" );
				mInfoWindowSize.X = Math.Max( mInfoWindowSize.X, ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X );
				if( WantToDeleteSelectedPreset )
				{
					ImGui.Button( "Don't do it!" );
				}
				mInfoWindowSize.Y = ImGui.GetItemRectMax().Y - ImGui.GetWindowPos().Y + ImGui.GetStyle().WindowPadding.Y;
			}

			ImGui.PopStyleVar();

			ImGui.End();
		}

		protected void DrawHelpWindow()
		{
			if( !HelpWindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSizeConstraints( new( Math.Max( 200f, mHelpWindowMinWidth ), 300f ), new( float.MaxValue ) );
			ImGuiHelpers.SetNextWindowPosRelativeMainViewport( ImGuiHelpers.MainViewport.Size / 3f, ImGuiCond.FirstUseEver );
			if( ImGui.Begin( Loc.Localize( "Window Title: Waymark Help", "Waymark Help" ) + "###Waymark Help", ref mHelpWindowVisible ) )
			{
				var cachedCurrentHelpPage = mCurrentHelpPage;
				for( int i = 0; i < Enum.GetValues( typeof( HelpWindowPage ) ).Length; ++i )
				{
					if( i > 0 ) ImGui.SameLine();
					if( i == (int)cachedCurrentHelpPage )
					{
						ImGui.PushStyleColor( ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered] );
					}
					if( ImGui.Button( Enum.GetNames( typeof( HelpWindowPage ) )[i] ) )
					{
						mCurrentHelpPage = (HelpWindowPage)i;
					}
					if( i == (int)cachedCurrentHelpPage )
					{
						ImGui.PopStyleColor();
					}
				}
				mHelpWindowMinWidth = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X;

				if( ImGui.BeginChild( "###Help Text Pane" ) )
				{
					ImGui.PushTextWrapPos( ImGui.GetWindowContentRegionWidth() );
					switch( mCurrentHelpPage )
					{
						case HelpWindowPage.General: DrawHelpWindow_General(); break;
						case HelpWindowPage.Editing: DrawHelpWindow_Editing(); break;
						case HelpWindowPage.Maps: DrawHelpWindow_Maps(); break;
						case HelpWindowPage.Coordinates: DrawHelpWindow_Coordinates(); break;
						case HelpWindowPage.CircleCalculator: DrawHelpWindow_CircleCalculator(); break;
						default: DrawHelpWindow_General(); break;
					};
					ImGui.PopTextWrapPos();
					ImGui.EndChild();
				}
			}

			ImGui.End();
		}

		protected void DrawHelpWindow_General()
		{
			ImGui.Text( Loc.Localize( "Help Window Text: General 1",
						"All presets in this plugin's list are fully separate from the game's presets.  This allows you to store an effectively " +
						"unlimited number of presets, as well as to easily back up and share them, or import presets that others have shared with you." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 2",
						"Selecting a preset in the library will show a window to the side with information about that preset, such as where the waymarks " +
						"are placed, as well as actions that you can take with that preset." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 3",
						"If you want to copy a preset in the " +
						"library to a game slot, select that preset in the list, and then press the button with the number of the slot to " +
						"which you want to copy it.  If you want to import a preset from the game's list into the library, scroll down to " +
						"\"Import Options\" and press the button of the slot that you wish to import from the game.  This is also where you " +
						"can paste presets to import them from outside of the game." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 4",
						"If you wish to share a preset with someone else, you can select the preset in the library, and " +
						"click the \"Export to Clipboard\" button." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 5",
						"The plugin also allows you to place and save waymarks directly to/from the field.  These are what the \"Place\" and " +
						"\"Save Current Waymarks\" buttons do.  Please note that saving and placing presets is only supported in areas that " +
						"the game allows with its built in system.  Saving presets outside of those duties will result in a preset that shows an " +
						"unknown zone.  Trying to place presets outside of those duties will simply fail to do anything." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: General 6",
						"Presets can be reordered in the library by dragging and dropping them at the desired spot.  The sorting of duties in " +
						"the library cannot currently be changed; the order is the same as they are sorted in the game's files, and is approximately " +
						"the order in which the duties were added to the game." ) );
		}

		protected void DrawHelpWindow_Editing()
		{
			ImGui.Text( Loc.Localize( "Help Window Text: Editing 1",
						"Clicking the \"Edit\" button in the preset info pane will bring up a window that allows you to " +
						"edit a preset.  You can adjust any of the available parameters, and you can drag waymarks on to " +
						"other waymarks to swap their positions.  You can also drag points from the circle calculator tab " +
						"in this help window on to a waymark in the editor window to replace its coordinates with the ones from that calculator." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: Editing 2",
						"Changes made in the editor window will not be applied until the \"Save\" button is clicked." ) );

			/*ImGui.Text( "Clicking the \"Edit\" button in the preset info pane will bring up a window that allows you to " +
						"edit a preset.  You can adjust any of the available parameters, and you can drag waymarks on to " +
						"other waymarks to swap their positions.  You can also drag points from the" );
			//ImGui.SameLine();
			ImGuiUtils.TextLink( () => { ShowHelpWindow( HelpWindowPage.CircleComputer ); }, "circle calculator tab" );
			//ImGui.SameLine();
			ImGui.Text( "on to a waymark in the editor window to replace its coordinates with the ones from that calculator." );*/
		}

		protected void DrawHelpWindow_Maps()
		{
			ImGui.Text( Loc.Localize( "Help Window Text: Maps 1",
						"The \"Map View\" window displays a copy of the applicable map(s) for the selected preset's duty.  Any placed " +
						"waymarks are overlain on the map.  If a zone has multiple submaps, you can switch between submaps using the dropdown " +
						"in the lower right corner of the window.  The world cordinates corresponding to your cursor position on the map are " +
						"shown at the bottom right of the window.  Please read the \"Coordinates\" tab of this help window if you wish to understand " +
						"the game's internal coordinate systems, and their relationship to what is presented in-game to the player." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: Maps 2",
						"When editing a preset, you can drag waymark icons on the map to adjust their positions.  While you are doing this, the " +
						"coordinate readout reflects the position of the marker, and not the position of your mouse.  Please note that editing " +
						"waymarks in this manner is not advised in areas that have uneven ground, as it is not possible to automatically adjust " +
						"the Y coordinate to match the terrain." ) );
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Text( Loc.Localize( "Help Window Text: Maps 3",
						"Please also note that the plugin cannot currently determine which waymarks are present on which submaps, so all waymarks " +
						"are shown at their positions on all submaps (provided that they are within the map's bounds)." ) );
		}

		protected void DrawHelpWindow_Coordinates()
		{
			ImGui.Text( Loc.Localize( "Help Window Text: Coordinates 1", "Coordinate Systems:" ) + "\r\n" );
			ImGui.Spacing();
			ImGui.Indent();
			ImGui.Text( Loc.Localize( "Help Window Text: Coordinates 2",
						"The game internally uses a right-handed 3D coordinate system, " +
						"with X running West to East, Y running down to up, and Z running North to South.  The on-map " +
						"coordinate system is a 2D projection of the XZ plane, with X running West to East, and Y running " +
						"North to South.  Please note that the coordinates presented in chat links or on the map widgets " +
						"in game are scaled to arbitrary values, and the Y and Z axes are swapped.  This plugin uses the " +
						"game's internal coordinate systems as shown below:" ) );
			ImGui.Unindent();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			if( mCoordinateSystemsDiagram != null )
			{
				const float imgWidthScale = 0.75f;
				const float imguiPaddingScale = 1.0f - imgWidthScale;
				ImGui.Indent( ImGui.GetWindowContentRegionWidth() * imguiPaddingScale / 2f );
				var size = new Vector2( mCoordinateSystemsDiagram.Width, mCoordinateSystemsDiagram.Height );
				size *= ImGui.GetWindowContentRegionWidth() / mCoordinateSystemsDiagram.Width * imgWidthScale;
				ImGui.Image( mCoordinateSystemsDiagram.ImGuiHandle, size );
				ImGui.Unindent();
			}
		}

		protected void DrawHelpWindow_CircleCalculator()
		{
			ImGui.Text( Loc.Localize( "Circle Computer Text: Instructions 1",
						"This calculator will compute radially symmetric points (\"clock spots\") with the information that you " +
						"give it.  You can then drag these into the preset editor to replace any waymarks with the calculated points, " +
						"or you can use the buttons at the bottom of this pane." ) );
			
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();

			ImGui.InputFloat3( Loc.Localize( "Circle Computer Text: Center Position", "Center Position" ) + "###Center Position", ref mCircleComputer_Center );
			ImGui.InputFloat( Loc.Localize( "Circle Computer Text: Radius", "Radius (y)" ) + "###Radius (y)", ref mCircleComputer_Radius_Yalms );
			ImGui.SliderInt( Loc.Localize( "Circle Computer Text: Number of Points", "Number of Points" ) + "##Number of Points", ref mCircleComputer_NumPoints, 1, 8 );
			ImGui.InputFloat( Loc.Localize( "Circle Computer Text: Angle Offset", "Angle Offset (deg)" ) + "###Angle Offset (deg)", ref mCircleComputer_AngleOffset_Deg );

			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();

			var points = ComputeRadialPositions( mCircleComputer_Center, mCircleComputer_Radius_Yalms, mCircleComputer_NumPoints, mCircleComputer_AngleOffset_Deg );

			for( int i = 0; i < 8; ++i )
			{
				if( i < points.Length )
				{
					ImGui.Selectable( $"{i + 1}: {points[i].X:F3}, {points[i].Y:F3}, {points[i].Z:F3}" );
					if( ImGui.BeginDragDropSource( ImGuiDragDropFlags.None ) )
					{
						ImGui.SetDragDropPayload( $"EditPresetCoords", mpEditWaymarkCoordDragAndDropData, (uint)Marshal.SizeOf<Vector3>() );
						Marshal.StructureToPtr( points[i], mpEditWaymarkCoordDragAndDropData, true );
						ImGui.Text( Loc.Localize( "Drag and Drop Preview: Edit Swap Waymark", "Copy coordinates to..." ) );
						ImGui.EndDragDropSource();
					}
				}
				else
				{
					ImGui.Text( "---" );
				}
			}

			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			ImGui.Spacing();
			//***** TODO: Maybe draw a diagram of these points. *****

			string copyIntoEditorButtonText = Loc.Localize( "Button: Copy Points from Circle Computer", "Copy these points into the editor" );
			if( EditingPreset )
			{
				if( ImGui.Button( copyIntoEditorButtonText + "###Copy these points into the editor button" ) )
				{
					for( int i = 0; i < points.Length && i < 8; ++i )
					{
						ScratchEditingPreset.SetWaymark( i, true, points[i] );
					}
				}
			}
			else
			{
				ImGui.PushStyleColor( ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] * 0.5f );
				ImGui.PushStyleColor( ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] );
				ImGui.PushStyleColor( ImGuiCol.ButtonActive, ImGui.GetStyle().Colors[(int)ImGuiCol.Button] );
				ImGui.PushStyleColor( ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled] );

				ImGui.Button( copyIntoEditorButtonText + "###Copy these points into the editor button" );

				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
				ImGui.PopStyleColor();
			}

			if( ImGui.Button( Loc.Localize( "Button: Create Preset from Circle Computer", "Create a new preset using these points" ) + "###Create a new preset using these points" ) )
			{
				WaymarkPreset newPreset = new();
				newPreset.Name = Loc.Localize( "Default Preset Name - Circle Computer", "Imported from Circle Computer" );
				for( int i = 0; i < points.Length && i < 8; ++i )
				{
					newPreset[i].Active = true;
					newPreset[i].SetCoords( points[i] );
				}

				int newPresetIndex = mConfiguration.PresetLibrary.ImportPreset( newPreset );
				if( !EditingPreset && newPresetIndex >= 0 && newPresetIndex < mConfiguration.PresetLibrary.Presets.Count )
				{
					SelectedPreset = newPresetIndex;	//***** TODO: Make sure that this doesn't break if the zone filter is causing it to not be in the list. *****
				}
			}
		}

		protected Vector3[] ComputeRadialPositions( Vector3 center, float radius_Yalms, int numPoints, float angleOffset_Deg = 0f )
		{
			//	Can't have less than one point (even that doesn't make much sense, but it's technically allowable).
			numPoints = Math.Max( 1, numPoints );
			var computedPoints = new Vector3[numPoints];

			//	Zero azimuth is facing North (90 degrees)
			angleOffset_Deg -= 90f;
			double stepAngle_Deg = 360.0 / numPoints;

			//	Compute the coordinates on the circle about the center point.
			for( int i = 0; i < numPoints; ++i )
			{
				//	Because of FFXIV's coordinate system, we need to go backward in angle.
				double angle_Rad = ( i * stepAngle_Deg + angleOffset_Deg ) * Math.PI / 180.0;
				computedPoints[i].X = (float)Math.Cos( angle_Rad );
				computedPoints[i].Z = (float)Math.Sin( angle_Rad );
				computedPoints[i] *= radius_Yalms;
				computedPoints[i] += center;
			}

			return computedPoints;
		}

		protected Vector3 mCircleComputer_Center = Vector3.Zero;
		protected float mCircleComputer_Radius_Yalms = 20f;
		protected int mCircleComputer_NumPoints = 8;
		protected float mCircleComputer_AngleOffset_Deg = 0f;

		protected void DrawEditorWindow()
		{
			if( EditingPresetIndex < 0 || EditingPresetIndex >= mConfiguration.PresetLibrary.Presets.Count )
			{
				return;
			}

			if( ImGui.Begin( Loc.Localize( "Window Title: Preset Editor", "Preset Editor" ) + "###Preset Editor", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize ) )
			{
				ImGuiUtils.TitleBarHelpButton( () => { ShowHelpWindow( HelpWindowPage.Editing ); }, 0, UiBuilder.IconFont );

				if( ScratchEditingPreset != null )
				{
					ImGui.Text( Loc.Localize( "Edit Window Text: Name", "Name: " ) );
					ImGui.SameLine();
					ImGui.InputText( "##PresetName", ref ScratchEditingPreset.Name, 128 );
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.BeginGroup();
					if( ImGui.BeginTable( "###PresetEditorWaymarkTable", 4 ) )
					{
						float numberWidth = ImGui.CalcTextSize( "-0000.000" ).X;
						string activeColumnHeaderText = Loc.Localize( "Edit Window Text: Active Column Header", "Active" );
						ImGui.TableSetupColumn( activeColumnHeaderText, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize( activeColumnHeaderText + "         " ).X + ImGui.GetStyle().CellPadding.X * 2 );
						ImGui.TableSetupColumn( "X", ImGuiTableColumnFlags.WidthFixed, numberWidth + ImGui.GetStyle().CellPadding.X * 2 + ImGui.GetStyle().FramePadding.X * 4 );
						ImGui.TableSetupColumn( "Y", ImGuiTableColumnFlags.WidthFixed, numberWidth + ImGui.GetStyle().CellPadding.X * 2 + ImGui.GetStyle().FramePadding.X * 4 );
						ImGui.TableSetupColumn( "Z", ImGuiTableColumnFlags.WidthFixed, numberWidth + ImGui.GetStyle().CellPadding.X * 2 + ImGui.GetStyle().FramePadding.X * 4 );
						ImGui.TableHeadersRow();
						
						foreach( var waymark in ScratchEditingPreset.Waymarks )
						{
							ImGui.TableNextRow();
							ImGui.TableSetColumnIndex( 0 );
							ImGui.Checkbox( $"{waymark.Label}             ###{waymark.Label}", ref waymark.Active );	//Padded text to make more area to grab the waymark for drag and drop.
							if( ImGui.BeginDragDropSource( ImGuiDragDropFlags.None ) )
							{
								ImGui.SetDragDropPayload( $"EditPresetWaymark", mpEditWaymarkDragAndDropData, sizeof( int ) );
								Marshal.WriteInt32( mpEditWaymarkDragAndDropData, waymark.ID );
								ImGui.Text( String.Format( Loc.Localize( "Drag and Drop Preview: Edit Swap Waymark", "Swap Waymark {0} with..." ), waymark.Label ) );
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
									payload = ImGui.AcceptDragDropPayload( $"EditPresetCoords", ImGuiDragDropFlags.None );
									if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
									{
										ScratchEditingPreset.SetWaymark( waymark.ID, true, *(Vector3*)payload.Data );
									}
								}
								ImGui.EndDragDropTarget();
							}
							ImGui.TableSetColumnIndex( 1 );
							ImGui.SetNextItemWidth( numberWidth + ImGui.GetStyle().FramePadding.X * 2 );
							ImGui.InputFloat( $"##{waymark.Label}-X", ref waymark.X );
							ImGui.TableSetColumnIndex( 2 );
							ImGui.SetNextItemWidth( numberWidth + ImGui.GetStyle().FramePadding.X * 2 );
							ImGui.InputFloat( $"##{waymark.Label}-Y", ref waymark.Y );
							ImGui.TableSetColumnIndex( 3 );
							ImGui.SetNextItemWidth( numberWidth + ImGui.GetStyle().FramePadding.X * 2 );
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
					if( ImGui.Button( Loc.Localize( "Button: Save", "Save" ) + "###Save Button" ) )
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
				if( ImGui.Button( Loc.Localize( "Button: Cancel", "Cancel" ) + "###Cancel Button" ) )
				{
					EditingPresetIndex = -1;
					ScratchEditingPreset = null;
				}
				ImGui.SameLine( ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize( "Map View" ).X - ImGui.GetStyle().FramePadding.X * 2 );
				if( ImGui.Button( Loc.Localize( "Button: Map View", "Map View" ) + "###Map View Button" ) )
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

			if( ImGui.Begin( Loc.Localize( "Window Title: Config", "Waymark Settings" ) + "###Waymark Settings", ref mSettingsWindowVisible,
				ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse ) )
			{
				ImGui.Checkbox( Loc.Localize( "Config Option: Always Show Info Pane", "Always show preset info pane." ) + "###Always show preset info pane checkbox", ref mConfiguration.mAlwaysShowInfoPane );
				ImGui.Checkbox( Loc.Localize( "Config Option: Clicking Preset Unselects", "Clicking the selected preset unselects it." ) + "###Clicking the selected preset unselects it checkbox", ref mConfiguration.mAllowUnselectPreset );
				ImGui.Checkbox( Loc.Localize( "Config Option: Categorize Presets by Zone", "Categorize presets by zone." ) + "###Categorize Presets By Zone Checkbox", ref mConfiguration.mSortPresetsByZone );
				ImGui.Checkbox( Loc.Localize( "Config Option: Open and Close with Game Window", "Open and close library with the game's waymark window." ) + "###Open and Close With Game Window checkbox", ref mConfiguration.mOpenAndCloseWithFieldMarkerAddon );
				ImGui.Checkbox( Loc.Localize( "Config Option: Attach to Game Window", "Attach library window to the game's waymark window." ) + "###Attach library window to the game's waymark window checkbox.", ref mConfiguration.mAttachLibraryToFieldMarkerAddon );
				ImGui.Checkbox( Loc.Localize( "Config Option: Show ID in Zone Names", "Show ID numbers next to zone names." ) + "###Show ID numbers next to zone names checkbox.", ref mConfiguration.mShowIDNumberNextToZoneNames );
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Show ID in Zone Names", "Shows the internal Content Finder ID of the zone/duty in some places.  Generally only used for debugging." ) );
				ImGui.Checkbox( Loc.Localize( "Config Option: Show Preset Indices", "Show the index of the preset within the library." ) + "###Show the index of the preset within the library checkbox", ref mConfiguration.mShowLibraryIndexInPresetList );
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Show Preset Indices", "The primary use of this is if you need to know the preset index to use within a text command.  You can always leave this disabled if you only use the GUI." ) );
				/*ImGui.Checkbox( "Allow placement of waymarks client-side in overworld zones.", ref mConfiguration.mAllowClientSidePlacementInOverworldZones );
				ImGuiUtils.HelpMarker( "Lets the plugin attempt to place waymarks in overworld zones that do not function with the game's preset interface.  These will only be visible client-side, and not to other party/alliance members.  This is out of specification behavior for the game, so please read this plugin's readme before enabling." );*/
				ImGui.Checkbox( Loc.Localize( "Config Option: Autoload Presets from Libarary", "Autoload presets from library." ) + "###Autoload presets from library checkbox", ref mConfiguration.mAutoPopulatePresetsOnEnterInstance );
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Autoload presets from Library", "Automatically loads the first five presets that exist in the library for a zone when you load into it.  THIS WILL OVERWRITE THE GAME'S SLOTS WITHOUT WARNING, so please do not turn this on until you are certain that you have saved any data that you want to keep.  Consider using this with the auto-import option below to reduce the risk of inadvertent preset loss." ) );
				ImGui.Checkbox( Loc.Localize( "Config Option: Autosave Presets to Library", "Autosave presets to library." ) + "###Autosave Presets to Library Checkbox", ref mConfiguration.mAutoSavePresetsOnInstanceLeave );
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Autosave Presets to Library", "Automatically copies any populated game preset slots into the library upon exiting an instance." ) );
				ImGui.Checkbox( String.Format( Loc.Localize( "Config Option: Suppress Text Command Responses", "Suppress responses to text commands (besides \"{0}\")." ), Plugin.SubcommandName_Help ) + "###Suppress Command Responses Checkbox", ref mConfiguration.mSuppressCommandLineResponses );
				if( ImGui.Button( Loc.Localize( "Button: Clear All Map View Data", "Clear All Map View Data" ) + "###Clear All Map View Data Button" ) )
				{
					ClearAllMapViewStateData();
				}
				ImGuiUtils.HelpMarker( Loc.Localize( "Help: Clear All Map View Data", "This deletes all map view pan/zoom/submap state, resetting every map back to default." ) );
				ImGui.Spacing();
				if( ImGui.Button( Loc.Localize( "Button: Save and Close", "Save and Close" ) + "###Save and Close Button" ) )
				{
					mConfiguration.Save();
					SettingsWindowVisible = false;
				}

				string showLibraryButtonString = Loc.Localize( "Button: Show Library", "Show Library" );
				ImGui.SameLine( ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize( showLibraryButtonString ).X - ImGui.GetStyle().FramePadding.X * 2 );
				if( ImGui.Button( showLibraryButtonString + "###Show Library Button" ) )
				{
					MainWindowVisible = true;
				}
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
			if( ImGui.Begin( ( showingEditingView ? Loc.Localize( "Window Title: Map View (Editing)", "Map View - Editing" ) : Loc.Localize( "Window Title: Map View", "Map View" ) ) + "###MapViewWindow", ref mMapWindowVisible,
				ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				//	Help button.
				ImGuiUtils.TitleBarHelpButton( ()=> { ShowHelpWindow(HelpWindowPage.Maps); }, 1, UiBuilder.IconFont );

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
								ImGui.Text( Loc.Localize( "Map Window Text: No Maps Available", "No maps available for this zone." ) );
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
									Vector2 mapLowerBounds = new( Math.Min( 1.0f, Math.Max( 0.0f, mapPan.X - mapZoom * 0.5f ) ), Math.Min( 1.0f, Math.Max( 0.0f, mapPan.Y - mapZoom * 0.5f ) ) );
									Vector2 mapUpperBounds = new( Math.Min( 1.0f, Math.Max( 0.0f, mapPan.X + mapZoom * 0.5f ) ), Math.Min( 1.0f, Math.Max( 0.0f, mapPan.Y + mapZoom * 0.5f ) ) );
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
									ImGui.Text( cursorPosText );
								}

								if( mapList.Count <= 1 || selectedSubMapIndex >= mapList.Count )
								{
									selectedSubMapIndex = 0;
								}
								else
								{
									float subMapComboWidth = 0.0f;
									List<string> subMaps = new();
									for( int i = 0; i < mapInfo.Length; ++i )
									{
										string subMapName = mapInfo[i].PlaceNameSub.Trim().Length < 1 ? String.Format( Loc.Localize( "Map Window Text: Unnamed Submap Placeholder", "Unnamed Sub-map {0}" ), i + 1 ) : mapInfo[i].PlaceNameSub;
										subMaps.Add( subMapName );
										subMapComboWidth = Math.Max( subMapComboWidth, ImGui.CalcTextSize( subMapName ).X );
									}
									subMapComboWidth += 40.0f;

									ImGui.SameLine( Math.Max( mapWidgetSize_Px /*- ImGui.CalcTextSize( cursorPosText ).X*/ - subMapComboWidth + 8, 0 ) );
									ImGui.SetNextItemWidth( subMapComboWidth );
									ImGui.Combo( "###SubmapDropdown", ref selectedSubMapIndex, subMaps.ToArray(), mapList.Count );
								}
							}
						}
						else
						{
							ImGui.Text( Loc.Localize( "Map Window Text: Loading Maps", "Loading zone map(s)." ) );
							LoadMapTextures( (UInt16)territoryTypeIDToShow );
						}

						mMapTextureDictMutex.ReleaseMutex();
					}
					else
					{
						ImGui.Text( Loc.Localize( "Map Window Text: Loading Maps", "Loading zone map(s)." ) );
					}
				}
				else
				{
					//***** TODO: The "else" message for this is a bit misleading; change this to be invalid zone selected, and make a separate one for actually no preset selected.
					ImGui.Text( Loc.Localize( "Map Window Text: No Preset Selected", "No Preset Selected" ) );
				}
			}

			ImGui.End();
		}

		protected Vector2 MapTextureCoordsToScreenCoords( Vector2 mapTextureCoords_Px, Vector2 mapVisibleLowerBounds_Norm, Vector2 mapVisibleUpperBounds_Norm, Vector2 mapViewportSize_Px, Vector2 mapViewportScreenPos_Px )
		{
			Vector2 newScreenCoords = mapTextureCoords_Px;
			newScreenCoords /= 2048.0f;
			newScreenCoords = ( newScreenCoords - mapVisibleLowerBounds_Norm ) / ( mapVisibleUpperBounds_Norm - mapVisibleLowerBounds_Norm ) * mapViewportSize_Px;
			newScreenCoords += mapViewportScreenPos_Px;

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
					PluginLog.Log( $"Error while copying preset data to game slot:\r\n{e}" );
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
									byte[] texData = MapTextureBlend( texFile.GetRgbaImageData(), parchmentTexFile?.GetRgbaImageData() );

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
			else if( mapScaleFactor > xValues[^1] )
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

		public void ShowHelpWindow( HelpWindowPage page )
		{
			mCurrentHelpPage = page;
			HelpWindowVisible = true;
		}

		public enum HelpWindowPage : int
		{
			General,
			Editing,
			Maps,
			Coordinates,
			CircleCalculator
		}

		protected HelpWindowPage mCurrentHelpPage = HelpWindowPage.General;

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

		protected bool mHelpWindowVisible = false;
		public bool HelpWindowVisible
		{
			get { return mHelpWindowVisible; }
			set { mHelpWindowVisible = value; }
		}

		protected string mPresetImportString = "";
		public string PresetImportString
		{
			get { return mPresetImportString; }
			set { mPresetImportString = value; }
		}

		public Vector2 MainWindowPos { get; protected set; }
		public Vector2 MainWindowSize { get; protected set; }
		protected Vector2 mInfoWindowSize;
		protected float mHelpWindowMinWidth;

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

		protected TextureWrap mCoordinateSystemsDiagram;

		protected Dictionary<UInt16, List<TextureWrap>> MapTextureDict { get; set; } = new();
		protected Mutex mMapTextureDictMutex = new();
		protected readonly TextureWrap[] mWaymarkIconTextures = new TextureWrap[8];
		protected int CapturedWaymarkIndex { get; set; } = -1;
		protected Vector2 CapturedWaymarkOffset { get; set; } = new( 0, 0 );
		protected static readonly Vector2 mWaymarkMapIconHalfSize_Px = new( 15, 15 );

		protected readonly IntPtr mpLibraryPresetDragAndDropData;
		protected readonly IntPtr mpEditWaymarkDragAndDropData;
		protected readonly IntPtr mpEditWaymarkCoordDragAndDropData;

		protected Dictionary<uint, MapViewState> MapViewStateData { get; set; } = new();
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

		public void SetWaymark( int index, bool active, Vector3 coords )
		{
			if( index >= 0 && index < Waymarks.Count )
			{
				Waymarks[index].Active = active;
				Waymarks[index].X = coords.X;
				Waymarks[index].Y = coords.Y;
				Waymarks[index].Z = coords.Z;
			}
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
			Waymarks = new();

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
			WaymarkPreset newPreset = new();

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
