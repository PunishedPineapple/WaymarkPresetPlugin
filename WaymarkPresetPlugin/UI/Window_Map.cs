using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

using Newtonsoft.Json;

using ImGuiNET;
using ImGuiScene;

using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Logging;
using Dalamud.Utility;

using CheapLoc;

namespace WaymarkPresetPlugin
{
	internal sealed class Window_Map : IDisposable
	{
		public Window_Map( PluginUI UI, DalamudPluginInterface pluginInterface, DataManager dataManager, Configuration configuration )
		{
			mUI = UI;
			mPluginInterface = pluginInterface;
			mDataManager = dataManager;
			mConfiguration = configuration;

			//	Try to read in the view state data.
			try
			{
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
		}

		public void Draw()
		{
			if( !WindowVisible )
			{
				return;
			}

			bool showingEditingView = mUI.EditorWindow.EditingPresetIndex > -1 && mUI.EditorWindow.ScratchEditingPreset != null;
			if( !showingEditingView )
			{
				CapturedWaymarkIndex = -1;  //	Shouldn't be necessary, but better to be safe than potentially muck up a preset.
			}
			ImGui.SetNextWindowSizeConstraints( new Vector2( 350, 380 ) * ImGui.GetIO().FontGlobalScale, new Vector2( int.MaxValue, int.MaxValue ) );
			if( ImGui.Begin( ( showingEditingView ? Loc.Localize( "Window Title: Map View (Editing)", "Map View - Editing" ) : Loc.Localize( "Window Title: Map View", "Map View" ) ) + "###MapViewWindow", ref mWindowVisible,
				ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				//	Help button.
				ImGuiUtils.TitleBarHelpButton( () => { mUI.HelpWindow.OpenHelpWindow( Window_Help.HelpWindowPage.Maps ); }, 1, UiBuilder.IconFont );

				//	Get TerritoryType ID of map to show, along with the (2D/XZ) zone coordinates of the waymarks.  Do this up front because we can be showing both normal presets or an editing scratch preset in the map view.
				uint territoryTypeIDToShow = 0;
				Vector2[] marker2dCoords = new Vector2[8];
				bool[] markerActiveFlags = new bool[8];
				if( showingEditingView )
				{
					territoryTypeIDToShow = ZoneInfoHandler.GetZoneInfoFromContentFinderID( mUI.EditorWindow.ScratchEditingPreset.MapID ).TerritoryTypeID;
					for( int i = 0; i < marker2dCoords.Length; ++i )
					{
						marker2dCoords[i] = new Vector2( mUI.EditorWindow.ScratchEditingPreset.Waymarks[i].X, mUI.EditorWindow.ScratchEditingPreset.Waymarks[i].Z );
						markerActiveFlags[i] = mUI.EditorWindow.ScratchEditingPreset.Waymarks[i].Active;
					}
				}
				else if( mUI.LibraryWindow.SelectedPreset > -1 && mUI.LibraryWindow.SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
				{
					territoryTypeIDToShow = ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].MapID ).TerritoryTypeID;
					marker2dCoords[0] = new Vector2( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].A.X, mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].A.Z );
					marker2dCoords[1] = new Vector2( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].B.X, mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].B.Z );
					marker2dCoords[2] = new Vector2( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].C.X, mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].C.Z );
					marker2dCoords[3] = new Vector2( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].D.X, mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].D.Z );
					marker2dCoords[4] = new Vector2( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].One.X, mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].One.Z );
					marker2dCoords[5] = new Vector2( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Two.X, mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Two.Z );
					marker2dCoords[6] = new Vector2( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Three.X, mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Three.Z );
					marker2dCoords[7] = new Vector2( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Four.X, mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Four.Z );

					markerActiveFlags[0] = mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].A.Active;
					markerActiveFlags[1] = mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].B.Active;
					markerActiveFlags[2] = mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].C.Active;
					markerActiveFlags[3] = mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].D.Active;
					markerActiveFlags[4] = mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].One.Active;
					markerActiveFlags[5] = mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Two.Active;
					markerActiveFlags[6] = mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Three.Active;
					markerActiveFlags[7] = mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Four.Active;
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
											Vector2 capturedMarkerPixelCoords = MapTextureCoordsToScreenCoords( mapInfo[selectedSubMapIndex].GetPixelCoordinates( marker2dCoords[CapturedWaymarkIndex] ),
																												mapLowerBounds,
																												mapUpperBounds,
																												new Vector2( mapWidgetSize_Px ),
																												mapWidgetScreenPos );

											capturedMarkerPixelCoords += mouseDragDelta;

											Vector2 capturedMarkerTexCoords = MapScreenCoordsToMapTextureCoords(    capturedMarkerPixelCoords,
																													mapLowerBounds,
																													mapUpperBounds,
																													new Vector2( mapWidgetSize_Px ),
																													mapWidgetScreenPos );

											marker2dCoords[CapturedWaymarkIndex] = mapInfo[selectedSubMapIndex].GetMapCoordinates( capturedMarkerTexCoords );

											if( mUI.EditorWindow.EditingPreset && mUI.EditorWindow.ScratchEditingPreset != null )
											{
												mUI.EditorWindow.ScratchEditingPreset.Waymarks[CapturedWaymarkIndex].X = marker2dCoords[CapturedWaymarkIndex].X;
												mUI.EditorWindow.ScratchEditingPreset.Waymarks[CapturedWaymarkIndex].Z = marker2dCoords[CapturedWaymarkIndex].Y;
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
											Vector2 waymarkMapPt = MapTextureCoordsToScreenCoords(  mapInfo[selectedSubMapIndex].GetPixelCoordinates( marker2dCoords[i] ),
																									mapLowerBounds,
																									mapUpperBounds,
																									new Vector2( mapWidgetSize_Px ),
																									mapWidgetScreenPos );

											ImGui.GetWindowDrawList().AddImage( mUI.WaymarkIconTextures[i].ImGuiHandle, waymarkMapPt - mWaymarkMapIconHalfSize_Px, waymarkMapPt + mWaymarkMapIconHalfSize_Px );

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

		private Vector2 MapTextureCoordsToScreenCoords( Vector2 mapTextureCoords_Px, Vector2 mapVisibleLowerBounds_Norm, Vector2 mapVisibleUpperBounds_Norm, Vector2 mapViewportSize_Px, Vector2 mapViewportScreenPos_Px )
		{
			Vector2 newScreenCoords = mapTextureCoords_Px;
			newScreenCoords /= 2048.0f;
			newScreenCoords = ( newScreenCoords - mapVisibleLowerBounds_Norm ) / ( mapVisibleUpperBounds_Norm - mapVisibleLowerBounds_Norm ) * mapViewportSize_Px;
			newScreenCoords += mapViewportScreenPos_Px;

			return newScreenCoords;
		}

		private Vector2 MapScreenCoordsToMapTextureCoords( Vector2 mapScreenCoords_Px, Vector2 mapVisibleLowerBounds_Norm, Vector2 mapVisibleUpperBounds_Norm, Vector2 mapViewportSize_Px, Vector2 mapViewportScreenPos_Px )
		{
			Vector2 newMapTexCoords = mapScreenCoords_Px;
			newMapTexCoords -= mapViewportScreenPos_Px;
			newMapTexCoords /= mapViewportSize_Px;
			newMapTexCoords *= mapVisibleUpperBounds_Norm - mapVisibleLowerBounds_Norm;
			newMapTexCoords += mapVisibleLowerBounds_Norm;
			newMapTexCoords *= 2048.0f;
			return newMapTexCoords;
		}

		private void LoadMapTextures( UInt16 territoryTypeID )
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

		private byte[] MapTextureBlend( byte[] mapTex, byte[] parchmentTex = null )
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

		private float GetDefaultMapZoom( float mapScaleFactor )
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

		public void ClearAllMapViewStateData()
		{
			MapViewStateData.Clear();
			string viewStateDataFilePath = Path.Join( mPluginInterface.GetPluginConfigDirectory(), $"\\MapViewStateData_v1.json" );
			if( File.Exists( viewStateDataFilePath ) )
			{
				File.Delete( viewStateDataFilePath );
			}
		}

		private bool mWindowVisible = false;
		public bool WindowVisible
		{
			get { return mWindowVisible; }
			set { mWindowVisible = value; }
		}

		private readonly PluginUI mUI;
		private readonly DalamudPluginInterface mPluginInterface;
		private readonly DataManager mDataManager;
		private readonly Configuration mConfiguration;

		private Dictionary<uint, MapViewState> MapViewStateData { get; set; } = new();
		private Dictionary<UInt16, List<TextureWrap>> MapTextureDict { get; set; } = new();
		private Mutex mMapTextureDictMutex = new();
		private int CapturedWaymarkIndex { get; set; } = -1;
		private Vector2 CapturedWaymarkOffset { get; set; } = new( 0, 0 );
		private static readonly Vector2 mWaymarkMapIconHalfSize_Px = new( 15, 15 );
	}
}
