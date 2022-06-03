using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

using CheapLoc;

using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Plugin;

using FFXIVClientStructs.FFXIV.Component.GUI;

using ImGuiNET;

using Newtonsoft.Json;


namespace WaymarkPresetPlugin
{
	internal sealed class Window_Library : IDisposable
	{
		public Window_Library( PluginUI UI, DalamudPluginInterface pluginInterface, GameGui gameGui, Configuration configuration, ClientState clientState, IntPtr pLibraryZoneDragAndDropData, IntPtr pLibraryPresetDragAndDropData )
		{
			mUI = UI;
			mPluginInterface = pluginInterface;
			mGameGui = gameGui;
			mConfiguration = configuration;
			mClientState = clientState;
			mpLibraryZoneDragAndDropData = pLibraryZoneDragAndDropData;
			mpLibraryPresetDragAndDropData = pLibraryPresetDragAndDropData;

			//	Try to read in the zone sort data.
			try
			{
				string zoneSortDataFilePath = Path.Join( mPluginInterface.GetPluginConfigDirectory(), mZoneSortDataFileName_v1 );
				string jsonStr = File.ReadAllText( zoneSortDataFilePath );
				var sortData = JsonConvert.DeserializeObject<List<UInt16>>( jsonStr );
				if( sortData != null ) mConfiguration.PresetLibrary.SetSortOrder( sortData );
			}
			catch( Exception e )
			{
				PluginLog.LogWarning( $"Unable to load library zone sort data:\r\n{e}" );
			}
		}

		public void Dispose()
		{
			//	Try to save off the zone sort data if we have any.
			WriteZoneSortDataToFile();
		}

		public void Draw()
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
				WindowVisible = false;
			}
			else if( mConfiguration.OpenAndCloseWithFieldMarkerAddon && !FieldMarkerAddonWasOpen && fieldMarkerAddonVisible )
			{
				WindowVisible = true;
			}
			FieldMarkerAddonWasOpen = fieldMarkerAddonVisible;

			if( !WindowVisible )
			{
				return;
			}

			//	Draw the window.
			if( mConfiguration.AttachLibraryToFieldMarkerAddon && fieldMarkerAddonVisible ) ImGui.SetNextWindowPos( dockedWindowPos );
			ImGui.SetNextWindowSize( new Vector2( 375, 375 ) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 375 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( Loc.Localize( "Window Title: Waymark Library", "Waymark Library" ) + "###Waymark Library", ref mWindowVisible, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar ) )
			{
				ImGuiUtils.TitleBarHelpButton( () => { mUI.HelpWindow.OpenHelpWindow( HelpWindowPage.General ); }, 1, UiBuilder.IconFont );

				DrawWaymarkButtons();

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

				// The string to use for filtering the list of zones
				var zoneFilterString = "";

				// Show the search text box when not filtering on current zone
				if( mConfiguration.ShowLibraryZoneFilterBox && !mConfiguration.FilterOnCurrentZone && mConfiguration.SortPresetsByZone )
				{
					ImGui.PushItemWidth( ImGui.CalcTextSize( "_" ).X * 20u );
					ImGui.InputText( Loc.Localize( "Library Window Text: Zone Search Label", "Search Zones" ) + "###Zone Filter Text Box", ref mSearchText, 16u );
					ImGui.PopItemWidth();

					zoneFilterString = mSearchText;
				}
				
				ImGui.BeginChild( "###Library Preset List Child Window" );

				(int,int,bool)? presetDragDropResult = null;
				(UInt16,UInt16)? zoneDragDropResult = null;
				ImGui.BeginGroup();
				if( mConfiguration.PresetLibrary.Presets.Count > 0 )
				{
					if( mConfiguration.mSortPresetsByZone )
					{
						bool anyPresetsVisibleWithCurrentFilters = false;
						mConfiguration.PresetLibrary.SortZonesDescending( mConfiguration.SortZonesDescending );
						var dict = mConfiguration.PresetLibrary.GetSortedIndices( true );
						/*if( mConfiguration.SortZonesDescending )
						{
							var tempZoneResult = DrawZoneDragDropTopOrBottomPlaceholder( true ); //***** TODO: Not using this for now because having this make the list move down feels pretty bad.
							zoneDragDropResult ??= tempZoneResult;
						}*/
						foreach( var zone in dict )
						{
							if( !mConfiguration.FilterOnCurrentZone || zone.Key == ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( mClientState.TerritoryType ) )
							{
								var zoneInfo = ZoneInfoHandler.GetZoneInfoFromContentFinderID( zone.Key );

								if( IsZoneFilteredBySearch( zoneFilterString, zoneInfo ) )
								{
									anyPresetsVisibleWithCurrentFilters = true;

									if( ImGui.CollapsingHeader( zoneInfo.DutyName.ToString() ) )
									{
										var tempZoneResult = DoZoneDragAndDrop( zoneInfo );
										var tempPresetResult = DrawPresetsForZone( zone );
										zoneDragDropResult ??= tempZoneResult;
										presetDragDropResult ??= tempPresetResult;
									}
									else
									{
										var tempZoneResult = DoZoneDragAndDrop( zoneInfo );
										zoneDragDropResult ??= tempZoneResult;
									}
								}
							}
						}
						if( !mConfiguration.SortZonesDescending )
						{
							var tempZoneResult = DrawZoneDragDropTopOrBottomPlaceholder( false );
							zoneDragDropResult ??= tempZoneResult;
						}

						if( !anyPresetsVisibleWithCurrentFilters )
						{
							ImGui.Text( Loc.Localize( "Main Window Text: No Presets Found", "No presets match the current filter." ) );
						}
					}
					else
					{
						if( ImGui.CollapsingHeader( Loc.Localize( "Header: Presets", "Presets" ) + "###Presets" ) )
						{
							presetDragDropResult = DrawUncategorizedPresets();
						}
					}
				}
				else
				{
					ImGui.Text( Loc.Localize( "Main Window Text: Library Empty", "Preset library empty!" ) );
				}
				ImGui.EndGroup();

				//ImGuiHelpers.ScaledDummy( 20.0f );	//***** TODO: Replace excess spacings with scaled dummies.
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();

				if( ImGui.CollapsingHeader( Loc.Localize( "Header: Import Options", "Import Options" ) + "###Import Options" ) )
				{
					DrawImportSection();
				}
				
				if( ImGui.CollapsingHeader( Loc.Localize( "Header: Export and Backup Options", "Export/Backup Options" ) + "###Export/Backup Options" ) )
				{
					DrawExportSection();
				}

				ImGui.EndChild();

				//	Handle moving a zone header if the user wanted to.
				if( zoneDragDropResult != null )
				{
					//	If it's the first time someone is dragging and dropping, set the sort order to what's currently visible.
					if( !mConfiguration.PresetLibrary.GetSortOrder().Any() )
					{
						List<UInt16> baseSortOrder = new();
						foreach( var zone in mConfiguration.PresetLibrary.GetSortedIndices( mConfiguration.SortZonesDescending ) ) baseSortOrder.Add( zone.Key );
						mConfiguration.PresetLibrary.SetSortOrder( baseSortOrder, mConfiguration.mSortZonesDescending );
						PluginLog.LogDebug( "Tried to set up initial zone sort order." );
					}

					//	Modify the sort entry for the drag and drop.
					mConfiguration.PresetLibrary.AddOrChangeSortEntry( zoneDragDropResult.Value.Item1, zoneDragDropResult.Value.Item2 );
					PluginLog.LogDebug( $"Tried to move zone id {zoneDragDropResult.Value.Item1} in front of {zoneDragDropResult.Value.Item2}." );
				}

				//	Handle moving a preset now if the user wanted to.
				if( presetDragDropResult != null )
				{
					SelectedPreset = mConfiguration.PresetLibrary.MovePreset( presetDragDropResult.Value.Item1, presetDragDropResult.Value.Item2, presetDragDropResult.Value.Item3 );
					if( SelectedPreset == -1 )
					{
						PluginLog.LogDebug( $"Unable to move preset {presetDragDropResult.Value.Item1} to {( presetDragDropResult.Value.Item3 ? "after " : "" )}index {presetDragDropResult.Value.Item2}." );
					}
					else
					{
						PluginLog.LogDebug( $"Moved preset {presetDragDropResult.Value.Item1} to index {SelectedPreset}." );
						mConfiguration.Save();
					}
					Marshal.WriteInt32( mpLibraryPresetDragAndDropData, -1 );
				}
			}

			//	Store the position and size so that we can keep the companion info window in the right place.
			WindowPos = ImGui.GetWindowPos();
			WindowSize = ImGui.GetWindowSize();

			//	We're done.
			ImGui.End();
		}

		unsafe private (int,int,bool)? DrawPresetsForZone( KeyValuePair<UInt16, List<int>> zonePresets )
		{
			bool doDragAndDropMove = false;
			int indexToMove = -1;
			int indexToMoveTo = -1;
			bool moveToAfter = false;
			var indices = zonePresets.Value;
			for( int i = 0; i < indices.Count; ++i )
			{
				if( ImGui.Selectable( $"{mConfiguration.PresetLibrary.Presets[indices[i]].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + indices[i].ToString() + ")" : "" )}###_Preset_{indices[i]}", indices[i] == SelectedPreset ) )
				{
					//	It's probably a bad idea to allow the selection to change when a preset's being edited.
					if( !mUI.EditorWindow.EditingPreset )
					{
						if( mConfiguration.AllowUnselectPreset && indices[i] == SelectedPreset )
						{
							SelectedPreset = -1;
						}
						else
						{
							SelectedPreset = indices[i];
						}

						mUI.InfoPaneWindow.CancelPendingDelete();
					}
				}
				if( !mUI.EditorWindow.EditingPreset && mConfiguration.AllowPresetDragAndDropOrdering && ImGui.BeginDragDropSource( ImGuiDragDropFlags.SourceNoHoldToOpenOthers ) )
				{
					ImGui.SetDragDropPayload( $"PresetIdxZ{zonePresets.Key}", mpLibraryPresetDragAndDropData, sizeof( int ) );
					Marshal.WriteInt32( mpLibraryPresetDragAndDropData, indices[i] );
					ImGui.Text( Loc.Localize( "Drag and Drop Preview: Moving Preset", "Moving: " ) + $"{mConfiguration.PresetLibrary.Presets[indices[i]].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + indices[i].ToString() + ")" : "" )}" );
					ImGui.EndDragDropSource();
				}
				if( !mUI.EditorWindow.EditingPreset && mConfiguration.AllowPresetDragAndDropOrdering && ImGui.BeginDragDropTarget() )
				{
					ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"PresetIdxZ{zonePresets.Key}", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect );
					if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
					{
						if( payload.IsDelivery() )
						{
							indexToMove = Marshal.ReadInt32( payload.Data );
							indexToMoveTo = indices[i];
							doDragAndDropMove = true;
						}
						else
						{
							ImGuiUtils.AddOverline( new Vector4( 1.0f, 1.0f, 0.0f, 1.0f ), 3.0f );
						}
					}

					ImGui.EndDragDropTarget();
				}
			}

			if( !mUI.EditorWindow.EditingPreset &&
				mConfiguration.AllowPresetDragAndDropOrdering &&
				ImGui.GetDragDropPayload().NativePtr != null &&
				ImGui.GetDragDropPayload().IsDataType( $"PresetIdxZ{zonePresets.Key}" ) &&
				ImGui.GetDragDropPayload().Data != IntPtr.Zero )
			{
				int draggedIndex = Marshal.ReadInt32( mpLibraryPresetDragAndDropData );
				if( draggedIndex >= 0 && draggedIndex < mConfiguration.PresetLibrary.Presets.Count && mConfiguration.PresetLibrary.Presets[draggedIndex].MapID == zonePresets.Key )
				{
					ImGui.Selectable( Loc.Localize( "Drag and Drop Preview: Move to Bottom", "<Move To Bottom>" ) + "###<Move To Bottom>" );
					if( ImGui.BeginDragDropTarget() )
					{
						ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"PresetIdxZ{zonePresets.Key}", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect );
						if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
						{
							if( payload.IsDelivery() )
							{
								indexToMove = Marshal.ReadInt32( payload.Data );
								indexToMoveTo = indices.Last();
								moveToAfter = true;
								doDragAndDropMove = true;
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

			//	Return the drag and drop results.
			return doDragAndDropMove ? new( indexToMove, indexToMoveTo, moveToAfter ) : null;
		}

		unsafe private (int,int,bool)? DrawUncategorizedPresets()
		{
			bool doDragAndDropMove = false;
			int indexToMove = -1;
			int indexToMoveTo = -1;
			bool anyPresetsVisibleWithCurrentFilters = false;
			for( int i = 0; i < mConfiguration.PresetLibrary.Presets.Count; ++i )
			{
				if( !mConfiguration.FilterOnCurrentZone || mConfiguration.PresetLibrary.Presets[i].MapID == ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( mClientState.TerritoryType ) )
				{
					anyPresetsVisibleWithCurrentFilters = true;
					if( ImGui.Selectable( $"{mConfiguration.PresetLibrary.Presets[i].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + i.ToString() + ")" : "" )}###_Preset_{i}", i == SelectedPreset ) )
					{
						//	It's probably a bad idea to allow the selection to change when a preset's being edited.
						if( !mUI.EditorWindow.EditingPreset )
						{
							if( mConfiguration.AllowUnselectPreset && i == SelectedPreset )
							{
								SelectedPreset = -1;
							}
							else
							{
								SelectedPreset = i;
							}

							mUI.InfoPaneWindow.CancelPendingDelete();
						}
					}
					if( !mUI.EditorWindow.EditingPreset && mConfiguration.AllowPresetDragAndDropOrdering && ImGui.BeginDragDropSource( ImGuiDragDropFlags.SourceNoHoldToOpenOthers ) )
					{
						ImGui.SetDragDropPayload( $"PresetIdxAnyZone", mpLibraryPresetDragAndDropData, sizeof( int ) );
						Marshal.WriteInt32( mpLibraryPresetDragAndDropData, i );
						ImGui.Text( Loc.Localize( "Drag and Drop Preview: Moving Preset", "Moving: " ) + $"{mConfiguration.PresetLibrary.Presets[i].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + i.ToString() + ")" : "" )}" );
						ImGui.EndDragDropSource();
					}
					if( !mUI.EditorWindow.EditingPreset && mConfiguration.AllowPresetDragAndDropOrdering && ImGui.BeginDragDropTarget() )
					{
						ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"PresetIdxAnyZone", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect );
						if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
						{
							if( payload.IsDelivery() )
							{
								indexToMove = Marshal.ReadInt32( payload.Data );
								indexToMoveTo = i;
								doDragAndDropMove = true;
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

			if( !mUI.EditorWindow.EditingPreset &&
				mConfiguration.AllowPresetDragAndDropOrdering &&
				ImGui.GetDragDropPayload().NativePtr != null &&
				ImGui.GetDragDropPayload().IsDataType( $"PresetIdxAnyZone" ) &&
				ImGui.GetDragDropPayload().Data != IntPtr.Zero )
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
								doDragAndDropMove = true;
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

			if( !anyPresetsVisibleWithCurrentFilters )
			{
				ImGui.Text( Loc.Localize( "Main Window Text: No Presets Found", "No presets match the current filter." ) );
			}

			//	Return the drag and drop results.
			return doDragAndDropMove ? new( indexToMove, indexToMoveTo, false ) : null;
		}

		unsafe private (UInt16,UInt16)? DoZoneDragAndDrop( ZoneInfo zoneInfo )
		{
			bool doDragAndDropMove = false;
			UInt16 zoneIndexToMove = 0;
			UInt16 zoneIndexToMoveTo = 0;
			if( !mUI.EditorWindow.EditingPreset && mConfiguration.AllowZoneDragAndDropOrdering && ImGui.BeginDragDropSource( ImGuiDragDropFlags.SourceNoHoldToOpenOthers ) )
			{
				ImGui.SetDragDropPayload( $"PresetZoneHeader", mpLibraryZoneDragAndDropData, sizeof( UInt16 ) );
				*(UInt16*)mpLibraryZoneDragAndDropData = zoneInfo.ContentFinderConditionID;
				ImGui.Text( Loc.Localize( "Drag and Drop Preview: Moving Zone", "Moving: " ) + $"{zoneInfo.DutyName}" );
				ImGui.EndDragDropSource();
			}
			if( !mUI.EditorWindow.EditingPreset && mConfiguration.AllowZoneDragAndDropOrdering && ImGui.BeginDragDropTarget() )
			{
				ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"PresetZoneHeader", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect );
				if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
				{
					if( payload.IsDelivery() )
					{
						zoneIndexToMove = *(UInt16*)payload.Data;
						zoneIndexToMoveTo = zoneInfo.ContentFinderConditionID;
						doDragAndDropMove = true;
					}
					else
					{
						if( mConfiguration.SortZonesDescending )
						{
							ImGuiUtils.AddUnderline( new Vector4( 1.0f, 1.0f, 0.0f, 1.0f ), 3.0f );
						}
						else
						{
							ImGuiUtils.AddOverline( new Vector4( 1.0f, 1.0f, 0.0f, 1.0f ), 3.0f );
						}
					}
				}
				ImGui.EndDragDropTarget();
			}

			//	Return the drag and drop results.
			return doDragAndDropMove ? new( zoneIndexToMove, zoneIndexToMoveTo ) : null;
		}

		unsafe private (UInt16,UInt16)? DrawZoneDragDropTopOrBottomPlaceholder( bool isTop )
		{
			bool doDragAndDropMove = false;
			UInt16 zoneIndexToMove = 0;
			UInt16 zoneIndexToMoveTo = 0;
			if( !mUI.EditorWindow.EditingPreset &&
				mConfiguration.AllowZoneDragAndDropOrdering &&
				ImGui.GetDragDropPayload().NativePtr != null &&
				ImGui.GetDragDropPayload().IsDataType( $"PresetZoneHeader" ) &&
				ImGui.GetDragDropPayload().Data != IntPtr.Zero )
			{
				UInt16 draggedZone = *(UInt16*)mpLibraryZoneDragAndDropData;
				if( draggedZone >= 0 )
				{
					if( isTop )	ImGui.CollapsingHeader( Loc.Localize( "Drag and Drop Preview: Move to Top", "<Move To Top>" ) + "###<Move To Top>" );
					else		ImGui.CollapsingHeader( Loc.Localize( "Drag and Drop Preview: Move to Bottom", "<Move To Bottom>" ) + "###<Move To Bottom>" );
					if( ImGui.BeginDragDropTarget() )
					{
						ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload( $"PresetZoneHeader", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoDrawDefaultRect );
						if( payload.NativePtr != null && payload.Data != IntPtr.Zero )
						{
							if( payload.IsDelivery() )
							{
								zoneIndexToMove = draggedZone;
								zoneIndexToMoveTo = UInt16.MaxValue;
								doDragAndDropMove = true;
							}
							else
							{
								if( isTop )
								{
									ImGuiUtils.AddUnderline( new Vector4( 1.0f, 1.0f, 0.0f, 1.0f ), 3.0f );
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

			//	Return the drag and drop results.
			return doDragAndDropMove ? new( zoneIndexToMove, zoneIndexToMoveTo ) : null;
		}

		private void DrawImportSection()
		{
			ImGui.BeginGroup(); //Buttons don't seem to work under a header without being in a group.
			ImGui.InputTextWithHint( "##JSONImportTextBox", Loc.Localize( "Text Box Prompt: Import", "Paste a preset here and click \"Import\"." ), ref mPresetImportString, 1024 );   //Most exports max out around 500 characters with all waymarks, so this leaves heaps of room for a long name.
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

		private void DrawExportSection()
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
				mConfiguration.Save();
				WriteZoneSortDataToFile();
				mUI.MapWindow.WriteMapViewStateToFile();
				mConfiguration.BackupConfigFile();
				mConfiguration.BackupConfigFolderFile( mZoneSortDataFileName_v1[..mZoneSortDataFileName_v1.LastIndexOf( '.' )], mZoneSortDataFileName_v1[(mZoneSortDataFileName_v1.LastIndexOf( '.' )+1)..] );
				mConfiguration.BackupConfigFolderFile( Window_Map.mMapViewStateDataFileName_v1[..Window_Map.mMapViewStateDataFileName_v1.LastIndexOf( '.' )], Window_Map.mMapViewStateDataFileName_v1[( Window_Map.mMapViewStateDataFileName_v1.LastIndexOf( '.' )+1)..] );
			}
			ImGuiUtils.HelpMarker( Loc.Localize( "Help: Backup Current Config", "Copies the current config file to a backup folder in the Dalamud \"pluginConfigs\" directory." ) );
			ImGui.EndGroup();
		}

		private void DrawWaymarkButtons()
		{
			//***** TODO: Move to a separate window on the side probably if we every actually do this.
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
			}
			ImGui.SameLine();
			if( ImGui.Button( "Clear" ) )
			{
				mCommandManager.ProcessCommand( "/waymarks clear" );
			}*/
		}

		internal void WriteZoneSortDataToFile()
		{
			try
			{
				var sortData = mConfiguration.PresetLibrary.GetSortOrder();
				if( sortData.Any() )
				{
					string jsonStr = JsonConvert.SerializeObject( sortData );
					string zoneSortDataFilePath = Path.Join( mPluginInterface.GetPluginConfigDirectory(), mZoneSortDataFileName_v1 );
					File.WriteAllText( zoneSortDataFilePath, jsonStr );
				}
			}
			catch( Exception e )
			{
				PluginLog.LogWarning( $"Unable to save library zone sort data:\r\n{e}" );
			}
		}

		internal void ClearAllZoneSortData()
		{
			mConfiguration.PresetLibrary.ClearSortOrder();
			string zoneSortDataFilePath = Path.Join( mPluginInterface.GetPluginConfigDirectory(), mZoneSortDataFileName_v1 );
			if( File.Exists( zoneSortDataFilePath ) )
			{
				File.Delete( zoneSortDataFilePath );
			}
		}

		public void TrySetSelectedPreset( int presetIndex )
		{
			if( !mUI.EditorWindow.EditingPreset ) SelectedPreset = presetIndex;
		}

		public void TryDeselectPreset()
		{
			if( !mUI.EditorWindow.EditingPreset ) SelectedPreset = -1;
		}

		private bool mWindowVisible = false;
		public bool WindowVisible
		{
			get { return mWindowVisible; }
			set { mWindowVisible = value; }
		}

		public Vector2 WindowPos { get; private set; }
		public Vector2 WindowSize { get; private set; }
		
		private bool IsZoneFilteredBySearch( string zoneFilterString, ZoneInfo zoneInfo )
		{
			var matchingZones = LibraryWindowZoneSearcher.GetMatchingZones( zoneFilterString );
			
			return zoneFilterString.Length == 0 || matchingZones.Any( id => id == zoneInfo.ContentFinderConditionID );
		}

		private readonly PluginUI mUI;
		private readonly DalamudPluginInterface mPluginInterface;
		private readonly GameGui mGameGui;
		private readonly Configuration mConfiguration;
		private readonly ClientState mClientState;

		private string mPresetImportString = "";
		public string PresetImportString
		{
			get { return mPresetImportString; }
			set { mPresetImportString = value; }
		}

		public int SelectedPreset { get; private set; } = -1;

		private bool FieldMarkerAddonWasOpen { get; set; } = false;

		private readonly IntPtr mpLibraryZoneDragAndDropData;
		private readonly IntPtr mpLibraryPresetDragAndDropData;
		
		private ZoneSearcher LibraryWindowZoneSearcher { get; set; } = new ZoneSearcher();
		private string mSearchText = "";

		internal const string mZoneSortDataFileName_v1 = "LibraryZoneSortData_v1.json";
	}
}
