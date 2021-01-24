using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Policy;
using Newtonsoft.Json;
using System.Diagnostics.Eventing.Reader;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Data.LuminaExtensions;
using ImGuiScene;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;

namespace WaymarkPresetPlugin
{
	// It is good to have this be disposable in general, in case you ever need it
	// to do any cleanup
	public class PluginUI : IDisposable
	{
		//	Construction
		public PluginUI( Configuration configuration, DalamudPluginInterface pluginInterface )
		{
			mConfiguration = configuration;
			mPluginInterface = pluginInterface;
		}

		//	Destruction
		public void Dispose()
		{
			//	Try to do this nicely for 10 seconds, but then just brute force it to clean up as much as we can.
			mMapTextureDictMutex.WaitOne( 10000 );

			//	Clean up all of the map textures that we've loaded.
			foreach( var mapTexturesList in MapTextureDict )
			{
				foreach( var tex in mapTexturesList.Value )
				{
					if( tex != null )
					{
						tex.Dispose();
					}
				}
			}

			//	Release the mutex and dispose of it.
			mMapTextureDictMutex.ReleaseMutex();
			mMapTextureDictMutex.Dispose();
		}

		public void Initialize()
		{
			//	Get the field markers sheet so that we can look up the textures we need.  Get it in English specifically so that we can more reliably parse the rows for what we want.
			ExcelSheet<Lumina.Excel.GeneratedSheets.FieldMarker> fieldMarkerSheet = mPluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.FieldMarker>( Dalamud.ClientLanguage.English );

			//*****TODO: This needs to be picked back up and finished once we can actually get the real icons through lumina.*****
			//	Find the rows that we want and get the texture names from them.
			/*foreach( var row in fieldMarkerSheet.ToList() )
			{
				var match = Regex.Match( row.Unknown3, "Waymark ([A-Z0-9])" );
				if( match.Success )
				{
					try
					{
						var texFile = mPluginInterface.Data.GetIcon( row.Icon );
						if( texFile != null )
						{
							var tex = mPluginInterface.UiBuilder.LoadImageRaw( texFile.Data, texFile.Header.Width, texFile.Header.Height, 4 );
							if( tex != null && tex.ImGuiHandle != IntPtr.Zero )
							{
								WaymarkTextureDict.Add( match.Groups[0].Value.ToCharArray()[0], tex );
							}
						}
					}
					catch( Exception e )
					{
						throw new Exception( $"Error during UI initialization: Unable to load waymark icon texture: {e}" );
					}
				}
			}*/

			var paths = new List<string>
			{
				"ui/icon/061000/061241.tex", // A
				"ui/icon/061000/061242.tex", // B
				"ui/icon/061000/061243.tex", // C
				"ui/icon/061000/061247.tex", // D
				"ui/icon/061000/061244.tex", // 1
				"ui/icon/061000/061245.tex", // 2
				"ui/icon/061000/061246.tex", // 3
				"ui/icon/061000/061248.tex", // 4
			};

			for( int i = 0; i < 8; ++i )
			{
				//*****TODO: This probably needs to be modified once we can actually get the real icons through lumina.*****
				var texFile =  mPluginInterface.Data.GetFile( paths[i] );
				var imageDataBGRA = texFile.Data.Skip( 80 ).ToArray();
				var imageDataRGBA = new Byte[40 * 40 * 4];
				
				for( int j = 0; j < imageDataBGRA.Length; j += 4 )
				{
					imageDataRGBA[j]	 = imageDataBGRA[j + 2];
					imageDataRGBA[j + 1] = imageDataBGRA[j + 1];
					imageDataRGBA[j + 2] = imageDataBGRA[j];
					imageDataRGBA[j + 3] = imageDataBGRA[j + 3];
				}

				WaymarkIconTextures[i] = mPluginInterface.UiBuilder.LoadImageRaw( imageDataRGBA, 40, 40, 4 );
			}
		}

		public void Draw()
		{
			//	Draw the sub-windows.
			DrawMainWindow();
			DrawInfoWindow();
			DrawMapWindow();
			DrawEditorWindow();
			DrawSettingsWindow();
		}

		protected void DrawMainWindow()
		{
			if( !MainWindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 375, 340 ), ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 340 ), new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( "Waymark Library", ref mMainWindowVisible, ImGuiWindowFlags.NoCollapse ) )
			{
				if( mConfiguration.ShowFilterOnCurrentZoneCheckbox )
				{
					ImGui.Checkbox( "Filter on Current Zone", ref mFilterOnCurrentZone );
				}
				if( mConfiguration.AllowDirectPlacePreset )
				{
					ImGui.SameLine( ImGui.GetWindowWidth() - 163 );  //*****TODO: The magic number is cheap and hacky; actually get the button width if we can.*****
					if( ImGui.Button( "Save Current Waymarks" ) )
					{
						GamePreset currentWaymarks = new GamePreset();
						if( MemoryHandler.GetCurrentWaymarksAsPresetData( ref currentWaymarks ) )
						{
							mConfiguration.PresetLibrary.ImportPreset( currentWaymarks );
						}
					}
				}
				ImGui.BeginGroup();
				if( mConfiguration.PresetLibrary.Presets.Count > 0 )
				{
					if( mConfiguration.mSortPresetsByZone )
					{
						var dict = mConfiguration.PresetLibrary.GetSortedIndices();
						foreach( var zone in dict )
						{
							if( !FilterOnCurrentZone || zone.Key == ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( CurrentTerritoryTypeID ) )
							{
								if( ImGui.CollapsingHeader( mConfiguration.mShowDutyNames ? ZoneInfoHandler.GetZoneInfoFromContentFinderID( zone.Key ).DutyName.ToString() : ZoneInfoHandler.GetZoneInfoFromContentFinderID( zone.Key ).ZoneName.ToString() ) )
								{
									foreach( int index in zone.Value )
									{
										if( ImGui.Selectable( $"{mConfiguration.PresetLibrary.Presets[index].Name}{(mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + index.ToString() + ")" : "")}###_Preset_{index}", index == SelectedPreset ) )
										{
											//	It's probably a bad idea to allow the selection to change when a preset's being edited.
											if( EditingPresetIndex == -1 )
											{
												if( mConfiguration.AllowUnselectPreset && index == SelectedPreset )
												{
													SelectedPreset = -1;
												}
												else
												{
													SelectedPreset = index;
												}

												WantToDeleteSelectedPreset = false;
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
								if( !FilterOnCurrentZone || mConfiguration.PresetLibrary.Presets[i].MapID == ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( CurrentTerritoryTypeID ) )
								{
									if( ImGui.Selectable( $"{mConfiguration.PresetLibrary.Presets[i].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + i.ToString() + ")" : "" )}###_Preset_{i}", i == SelectedPreset ) )
									{
										//	It's probably a bad idea to allow the selection to change when a preset's being edited.
										if( EditingPresetIndex == -1 )
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
					ImGui.Text( "Preset:" );
					ImGui.SameLine();
					ImGui.InputText( "##JSONImportTextBox", ref mPresetImportString, 1024 );   //Most exports max out around 500 characters with all waymarks, so this leaves heaps of room for a long name.
					//ImGui.InputTextWithHint( "##JSONImportTextBox", "Paste Formatted Preset Here", mPresetImportString, 1024 );	//	InputTextWithHint seems to be unusable in the C# bindings due to buf not being a ref.
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
						ImGui.Text( "Or import from game: " );
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
					ImGui.EndGroup();
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

			ImGui.SetNextWindowSize( new Vector2( 250, 340 ) );
			ImGui.SetNextWindowPos( new Vector2( MainWindowPos.X + MainWindowSize.X, MainWindowPos.Y ) );
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
					if( mConfiguration.AllowDirectPlacePreset )
					{
						ImGui.SameLine();
						if( ImGui.Button( "Place" ) )
						{
							MemoryHandler.PlacePreset( mConfiguration.PresetLibrary.Presets[SelectedPreset].GetAsGamePreset(), mConfiguration.AllowClientSidePlacementInOverworldZones );
						}
					}

					ImGui.EndGroup();
					ImGui.Text( "Preset Info:" );
					ImGui.SameLine( ImGui.GetWindowWidth() - 79 );
					if( ImGui.Button( "Map View" ) )
					{
						MapWindowVisible = !MapWindowVisible;
					}
					ImGui.Text( mConfiguration.PresetLibrary.Presets[SelectedPreset].GetPresetDataString( mConfiguration.GetZoneNameDelegate, mConfiguration.ShowIDNumberNextToZoneNames ) );
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

			ImGui.SetNextWindowSize( new Vector2( 375, 425 ) );
			if( ImGui.Begin( "Preset Editor", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize ) )
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
					ImGui.Columns( 4 );
					ImGui.Text( "Active" );
					foreach( var waymark in ScratchEditingPreset.Waymarks )
					{
						ImGui.Checkbox( waymark.Label, ref waymark.Active );
					}
					ImGui.NextColumn();
					ImGui.Text( "X" );
					foreach( var waymark in ScratchEditingPreset.Waymarks )
					{
						ImGui.InputFloat( $"##{waymark.Label}-X", ref waymark.X );
					}
					ImGui.NextColumn();
					ImGui.Text( "Y" );
					foreach( var waymark in ScratchEditingPreset.Waymarks )
					{
						ImGui.InputFloat( $"##{waymark.Label}-Y", ref waymark.Y );
					}
					ImGui.NextColumn();
					ImGui.Text( "Z" );
					foreach( var waymark in ScratchEditingPreset.Waymarks )
					{
						ImGui.InputFloat( $"##{waymark.Label}-Z", ref waymark.Z );
					}
					ImGui.Columns( 1 );
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
				ImGui.SameLine( ImGui.GetWindowWidth() - 79 );  //*****TODO: The magic number is cheap and hacky; actually get the button width if we can.*****
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

			ImGui.SetNextWindowSize( new Vector2( 430, 370 ) );
			if( ImGui.Begin( "Waymark Settings", ref mSettingsWindowVisible,
				ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				ImGui.Checkbox( "Always show preset info pane.", ref mConfiguration.mAlwaysShowInfoPane );
				ImGui.Checkbox( "Clicking the selected preset unselects it.", ref mConfiguration.mAllowUnselectPreset );
				ImGui.Checkbox( "Categorize presets by zone.", ref mConfiguration.mSortPresetsByZone );
				ImGui.Checkbox( "Show duty names instead of zone names.", ref mConfiguration.mShowDutyNames );
				ImGui.Checkbox( "Show \"Filter on Current Zone\" checkbox.", ref mConfiguration.mShowFilterOnCurrentZoneCheckbox );
				ImGui.Checkbox( "Show ID numbers next to zone names.", ref mConfiguration.mShowIDNumberNextToZoneNames );
				ImGuiHelpMarker( "Shows the internal Content Finder ID of the zone/duty in some places.  Generally only used for debugging." );
				ImGui.Checkbox( "Show the index of the preset within the library.", ref mConfiguration.mShowLibraryIndexInPresetList );
				ImGuiHelpMarker( "The primary use of this is if you need to know the preset index to use within a text command.  You can always leave this disabled if you only use the GUI." );
				ImGui.Checkbox( "Allow placement/saving of presets directly.", ref mConfiguration.mAllowDirectPlacePreset );
				ImGuiHelpMarker( "Enables buttons to save and place presets to/from the library, bypassing the game's preset UI entirely.  Please read the plugin site's readme before enabling this." );
				if( !mConfiguration.AllowDirectPlacePreset ) mConfiguration.AllowClientSidePlacementInOverworldZones = false;
				ImGui.Indent();
					ImGui.Checkbox( "Allow placement of waymarks client-side in overworld zones.", ref mConfiguration.mAllowClientSidePlacementInOverworldZones );
					ImGuiHelpMarker( "Lets the plugin attempt to place waymarks in overworld zones that do not function with the game's preset interface.  These will only be visible client-side, and not to other party/alliance members.  This is out of specification behavior for the game, so please read this plugin's readme before enabling." );
				ImGui.Unindent();
				ImGui.Checkbox( "Autoload presets from library.", ref mConfiguration.mAutoPopulatePresetsOnEnterInstance );
				ImGuiHelpMarker( "Automatically loads the first five presets that exist in the library for a zone when you load into it.  THIS WILL OVERWRITE THE GAME'S SLOTS WITHOUT WARNING, so please do not turn this on until you are certain that you have saved any data that you want to keep.  Consider using this with the auto-import option below to reduce the risk of inadvertent preset loss." );
				ImGui.Checkbox( "Autosave presets to library.", ref mConfiguration.mAutoSavePresetsOnInstanceLeave );
				ImGuiHelpMarker( "Automatically copies any populated game preset slots into the library upon exiting an instance." );
				if( !mConfiguration.ShowFilterOnCurrentZoneCheckbox ) FilterOnCurrentZone = false;
				ImGui.Spacing();
				if( ImGui.Button( "Save and Close" ) )
				{
					mConfiguration.Save();
					SettingsWindowVisible = false;
				}
				ImGui.SameLine( ImGui.GetWindowWidth() - 90 );	//*****TODO: The magic number is cheap and hacky; actually get the button width if we can.*****
				if( ImGui.Button( "Show Library" ) )
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
			ImGui.SetNextWindowSizeConstraints( new Vector2( 350, 380 ), new Vector2( int.MaxValue, int.MaxValue ) );
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
								var mapList = MapTextureDict[(UInt16)territoryTypeIDToShow];
								var mapInfo = ZoneInfoHandler.GetMapInfoFromTerritoryTypeID( territoryTypeIDToShow );

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

									Vector2 windowSize = ImGui.GetWindowSize();
									float mapWidgetSize_Px = Math.Min( windowSize.X - 15, windowSize.Y - 60 );

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

									string cursorPosText = "X: ---, Y: ---";
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
										cursorPosText = $"X: {mapRealCoords.X.ToString( "0.00" )}, Y: {mapRealCoords.Y.ToString( "0.00" )}";
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
											
											ImGui.GetWindowDrawList().AddImage( WaymarkIconTextures[i].ImGuiHandle, waymarkMapPt - mWaymarkMapIconHalfSize_Px, waymarkMapPt + mWaymarkMapIconHalfSize_Px );

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
									if( mapInfo.Length > 1 )
									{
										string submapInfoString = $"Sub-Map: {mapInfo[selectedSubMapIndex].PlaceNameSub} ({selectedSubMapIndex + 1}/{mapInfo.Length})";
										ImGui.SameLine( mapWidgetSize_Px - ImGui.CalcTextSize( submapInfoString ).X );
										ImGui.Text( submapInfoString );
									}
								}
								//	Put the radio buttons down below since they'll not commonly be used.  Set the selected map index to zero first if applicable.
								if( mapList.Count <= 1 || selectedSubMapIndex >= mapList.Count )
								{
									selectedSubMapIndex = 0;
								}
								else
								{
									for( int i = 0; i < mapList.Count; ++i )
									{
										if( ImGui.RadioButton( $"{mapInfo[i].PlaceNameSub}##{i}SelectedMapIndex", i == selectedSubMapIndex ) )
										{
											selectedSubMapIndex = i;
										}
									}
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
								var texFile = mPluginInterface.Data.GetFile<Lumina.Data.Files.TexFile>( map.GetMapFilePath() );
								var parchmentTexFile = mPluginInterface.Data.GetFile<Lumina.Data.Files.TexFile>( map.GetMapParchmentImageFilePath() );
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

		protected void ImGuiHelpMarker( string description, bool sameLine = true, string marker = "(?)" )
		{
			if( sameLine ) ImGui.SameLine();
			ImGui.TextDisabled( marker );
			if( ImGui.IsItemHovered() )
			{
				ImGui.BeginTooltip();
				ImGui.PushTextWrapPos( ImGui.GetFontSize() * 35.0f );
				ImGui.TextUnformatted( description );
				ImGui.PopTextWrapPos();
				ImGui.EndTooltip();
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

		protected Configuration mConfiguration;

		protected DalamudPluginInterface mPluginInterface;

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

		protected string mPresetImportString = "";
		public string PresetImportString
		{
			get { return mPresetImportString; }
			set { mPresetImportString = value; }
		}

		protected bool mFilterOnCurrentZone = false;
		public bool FilterOnCurrentZone
		{
			get { return mFilterOnCurrentZone; }
			set { mFilterOnCurrentZone = value; }
		}

		public Vector2 MainWindowPos { get; protected set; }
		public Vector2 MainWindowSize { get; protected set; }

		public int SelectedPreset { get; protected set; } = -1;
		public bool WantToDeleteSelectedPreset { get; protected set; } = false;
		public int EditingPresetIndex { get; protected set; } = -1;
		protected  ScratchPreset ScratchEditingPreset { get; set; }
		protected UInt16 CurrentTerritoryTypeID { get; set; }
		protected ZoneSearcher EditWindowZoneSearcher { get; set; } = new ZoneSearcher();
		protected string mEditWindowZoneFilterString = "";
		protected bool EditWindowZoneComboWasOpen { get; set; } = false;
		protected Dictionary<UInt16, List<TextureWrap>> MapTextureDict { get; set; } = new Dictionary<UInt16, List<TextureWrap>>();
		protected Mutex mMapTextureDictMutex = new Mutex();
		//protected Dictionary<char, TextureWrap> WaymarkIconTextureDict { get; set; } = new Dictionary<char, TextureWrap>();
		protected TextureWrap[] WaymarkIconTextures { get; set; } = new TextureWrap[8];
		protected int CapturedWaymarkIndex { get; set; } = -1;
		protected Vector2 CapturedWaymarkOffset { get; set; } = new Vector2( 0, 0 );
		protected static readonly Vector2 mWaymarkMapIconHalfSize_Px = new Vector2( 15, 15 );

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
