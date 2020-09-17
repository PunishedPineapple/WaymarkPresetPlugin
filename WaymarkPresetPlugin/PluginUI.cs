using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Policy;
using Newtonsoft.Json;
using System.Diagnostics.Eventing.Reader;
using Lumina.Excel.GeneratedSheets;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Data.LuminaExtensions;
using ImGuiScene;
using System.Threading.Tasks;
using System.Threading;

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

		public void Draw()
		{
			//	Draw the sub-windows.
			DrawMainWindow();
			DrawInfoWindow();
			DrawEditorWindow();
			DrawSettingsWindow();
			DrawMapWindow();
		}

		protected void DrawMainWindow()
		{
			if( !MainWindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 375, 330 ), ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 330 ), new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( "Waymark Library", ref mMainWindowVisible, ImGuiWindowFlags.NoCollapse ) )
			{
				//	Populate the preset list
				if( mConfiguration.ShowFilterOnCurrentZoneCheckbox )
				{
					ImGui.Checkbox( "Filter on Current Zone", ref mFilterOnCurrentZone );
				}
				//*****TODO: Check if this should be enabled or not.
				ImGui.SameLine( ImGui.GetWindowWidth() - 163 );  //*****TODO: The magic number is cheap and hacky; actually get the button width if we can.*****
				if( ImGui.Button( "Save Current Waymarks" ) )
				{
					byte[] currentWaymarks = null;
					if( MemoryHandler.GetCurrentWaymarksAsPresetData( ref currentWaymarks ) && currentWaymarks != null )
					{
						mConfiguration.PresetLibrary.ImportPreset( currentWaymarks );
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

			ImGui.SetNextWindowSize( new Vector2( 375, 330 ) );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 250, 330 ), new Vector2( 250, MainWindowSize.Y ) );
			ImGui.SetNextWindowPos( new Vector2( MainWindowPos.X + MainWindowSize.X, MainWindowPos.Y ) );
			if( ImGui.Begin( "Preset Info", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize ) )
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
							MemoryHandler.DirectPlacePreset( mConfiguration.PresetLibrary.Presets[SelectedPreset].ConstructGamePreset() );
						}
					}

					ImGui.EndGroup();
					ImGui.Text( "Preset Info:" );
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

					//*****TODO: Display map window based on settings or place the button in a better place.*****
					if( ImGui.Button( "Map" ) )
					{
						MapWindowVisible = true;
					}
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
				WaymarkPreset editingPreset = mConfiguration.PresetLibrary.Presets[EditingPresetIndex];
				ImGui.Text( "Name: " );
				ImGui.SameLine();
				ImGui.InputText( "##PresetName", ref editingPreset.Name, 128 );
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.BeginGroup();
				ImGui.Columns( 2 );
				ImGui.SetColumnWidth(0, 50);
				ImGui.Text( "Active" );
				foreach( var waymark in editingPreset.Waymarks )
				{
					ImGui.Checkbox( waymark.Label, ref waymark.Active );
				}
				ImGui.NextColumn();
				ImGui.Text( "" );
				foreach( var waymark in editingPreset.Waymarks )
				{
					var val = new Vector3(waymark.X / 1000f, waymark.Y / 1000f, waymark.Z / 1000f);
					if (ImGui.InputFloat3( $"##{waymark.Label}-X", ref val ))
					{
						waymark.X = (int)(val.X * 1000f);
						waymark.Y = (int)(val.Y * 1000f);
						waymark.Z = (int)(val.Z * 1000f);
					}
				}
				ImGui.Columns( 1 );
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Spacing();
				ImGui.Text( "Zone: " );
				if( ImGui.BeginCombo( "##MapID", mConfiguration.GetZoneName(editingPreset.MapID ) ) )
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
						if( zoneID != 0 && ImGui.Selectable( mConfiguration.GetZoneName( zoneID ), zoneID == editingPreset.MapID ) )
						{
							editingPreset.MapID = zoneID;
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
						mConfiguration.Save();
					}
					catch
					{
					}
					EditingPresetIndex = -1;
				}
				ImGui.SameLine();
				

				if( ImGui.Button( "Cancel" ) )
				{
					EditingPresetIndex = -1;
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

			ImGui.SetNextWindowSize( new Vector2( 350, 350 ) );
			if( ImGui.Begin( "Waymark Settings", ref mSettingsWindowVisible,
				ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				ImGui.Checkbox( "Always show preset info pane.", ref mConfiguration.mAlwaysShowInfoPane );
				ImGui.Checkbox( "Clicking the selected preset unselects it.", ref mConfiguration.mAllowUnselectPreset );
				ImGui.Checkbox( "Categorize presets by zone.", ref mConfiguration.mSortPresetsByZone );
				ImGui.Checkbox( "Show duty names instead of zone names.", ref mConfiguration.mShowDutyNames );
				ImGui.Checkbox( "Show \"Filter on Current Zone\" checkbox.", ref mConfiguration.mShowFilterOnCurrentZoneCheckbox );
				ImGui.Checkbox( "Show ID numbers next to zone names.", ref mConfiguration.mShowIDNumberNextToZoneNames );
				ImGui.Checkbox( "Show the index of the preset within the library.", ref mConfiguration.mShowLibraryIndexInPresetList );
				ImGui.Checkbox( "Allow placement of presets directly from the library*.", ref mConfiguration.mAllowDirectPlacePreset );
                ImGui.Checkbox( "Autoload waymarks from preset library.", ref mConfiguration.mAutoPopulatePresetsOnEnterInstance);
                ImGui.Checkbox( "Autosave waymarks to preset library.", ref mConfiguration.mAutoSaveWaymarksOnInstanceLeave);
                ImGui.Text( "*Please read the plugin site's readme before enabling this." );
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

			//ImGui.SetNextWindowSize( new Vector2( 350, 350 ) );
			if( ImGui.Begin( "Map View", ref mMapWindowVisible,
				ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				if( SelectedPreset > -1 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
				{
					//	Try to show the map(s); otherwise, show a message that they're still loading.
					if( mMapTextureDictMutex.WaitOne( 0 ) )
					{
						//*****TODO: Actually handle multiple maps per zone.  Just doing the first one rn for testing.*****

						if( MapTextureDict.ContainsKey( (UInt16)ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID ).TerritoryTypeID ) )
						{
							if( MapTextureDict[(UInt16)ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID ).TerritoryTypeID].Count < 1 )
							{
								ImGui.Text( "No maps available for this zone." );
							}
							else
							{
								var mapList = MapTextureDict[(UInt16)ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID ).TerritoryTypeID];
								for( int i = 0; i < mapList.Count; ++ i )
								{
									var mapInfo = ZoneInfoHandler.GetMapInfoFromTerritoryTypeID( ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID ).TerritoryTypeID );
									if( ImGui.RadioButton( $"{i} - {mapInfo[i].GetMapFilePath()}##SelectedMapIndex", i == mSelectedMapIndex ) )
									{
										mSelectedMapIndex = i;
									}
								}
								//*****TODO: Reset the selected map index to zero when the zone of the preset is different than last time.*****
								if( mSelectedMapIndex < mapList.Count )
								{
									ImGui.Image( mapList[mSelectedMapIndex].ImGuiHandle, new Vector2( 512, 512 ), new Vector2( 0.25f, 0.25f ), new Vector2( 0.75f, 0.75f ), new Vector4( 1, 1, 1, 1 ), new Vector4( 1, 1, 1, 1 ) );
								}
							}
						}
						else
						{
							ImGui.Text( "Loading zone map(s)." );
							LoadMapTextures( (UInt16)ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[SelectedPreset].MapID ).TerritoryTypeID );
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

		protected void CopyPresetToGameSlot( WaymarkPreset preset, uint slot )
		{
			if( ZoneInfoHandler.IsKnownContentFinderID( preset.MapID ) && slot >= 1 && slot <= 5 )
			{
				byte[] gamePresetData = preset.ConstructGamePreset();
				if( gamePresetData.Length == 104 )
				{
					try
					{
						MemoryHandler.WriteSlot( slot, gamePresetData );
					}
					catch( Exception e )
					{
						PluginLog.Log( $"Error while copying preset data to game slot: {e}" );
					}
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
		public UInt16 CurrentTerritoryTypeID { get; set; }
		protected ZoneSearcher EditWindowZoneSearcher { get; set; } = new ZoneSearcher();
		protected string mEditWindowZoneFilterString = "";
		protected bool EditWindowZoneComboWasOpen { get; set; } = false;
		protected Dictionary<UInt16, List<TextureWrap>> MapTextureDict { get; set; } = new Dictionary<UInt16, List<TextureWrap>>();
		protected Mutex mMapTextureDictMutex = new Mutex();
		protected int mSelectedMapIndex = 0;
	}
}
