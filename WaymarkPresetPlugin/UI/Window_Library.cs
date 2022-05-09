﻿using System;
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
using Dalamud.Game.Gui;
using Dalamud.Game.ClientState;
using FFXIVClientStructs.FFXIV.Component.GUI;
using CheapLoc;


namespace WaymarkPresetPlugin
{
	internal sealed class Window_Library : IDisposable
	{
		public Window_Library( PluginUI UI, DalamudPluginInterface pluginInterface, GameGui gameGui, Configuration configuration, ClientState clientState, IntPtr pLibraryPresetDragAndDropData )
		{
			mUI = UI;
			mPluginInterface = pluginInterface;
			mGameGui = gameGui;
			mConfiguration = configuration;
			mClientState = clientState;
			mpLibraryPresetDragAndDropData = pLibraryPresetDragAndDropData;
		}

		public void Dispose()
		{

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
			if( ImGui.Begin( Loc.Localize( "Window Title: Waymark Library", "Waymark Library" ) + "###Waymark Library", ref mWindowVisible, ImGuiWindowFlags.NoCollapse ) )
			{
				ImGuiUtils.TitleBarHelpButton( () => { mUI.HelpWindow.OpenHelpWindow( HelpWindowPage.General ); }, 1, UiBuilder.IconFont );

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
							if( !mConfiguration.FilterOnCurrentZone || zone.Key == ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( mClientState.TerritoryType ) )
							{
								if( ImGui.CollapsingHeader( ZoneInfoHandler.GetZoneInfoFromContentFinderID( zone.Key ).DutyName.ToString() ) )
								{
									var indices = zone.Value;
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
										if( !mUI.EditorWindow.EditingPreset && ImGui.BeginDragDropSource( ImGuiDragDropFlags.None ) )
										{
											ImGui.SetDragDropPayload( $"PresetIdxZ{zone.Key}", mpLibraryPresetDragAndDropData, sizeof( int ) );
											Marshal.WriteInt32( mpLibraryPresetDragAndDropData, indices[i] );
											ImGui.Text( Loc.Localize( "Drag and Drop Preview: Moving Preset", "Moving: " ) + $"{mConfiguration.PresetLibrary.Presets[indices[i]].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + indices[i].ToString() + ")" : "" )}" );
											ImGui.EndDragDropSource();
										}
										if( !mUI.EditorWindow.EditingPreset && ImGui.BeginDragDropTarget() )
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
										if( !mUI.EditorWindow.EditingPreset && ImGui.GetDragDropPayload().NativePtr != null && ImGui.GetDragDropPayload().Data != IntPtr.Zero )
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
								if( !mConfiguration.FilterOnCurrentZone || mConfiguration.PresetLibrary.Presets[i].MapID == ZoneInfoHandler.GetContentFinderIDFromTerritoryTypeID( mClientState.TerritoryType ) )
								{
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
									if( !mUI.EditorWindow.EditingPreset && ImGui.BeginDragDropSource( ImGuiDragDropFlags.None ) )
									{
										ImGui.SetDragDropPayload( $"PresetIdxAnyZone", mpLibraryPresetDragAndDropData, sizeof( int ) );
										Marshal.WriteInt32( mpLibraryPresetDragAndDropData, i );
										ImGui.Text( Loc.Localize( "Drag and Drop Preview: Moving Preset", "Moving: " ) + $"{mConfiguration.PresetLibrary.Presets[i].Name}{( mConfiguration.ShowLibraryIndexInPresetInfo ? " (" + i.ToString() + ")" : "" )}" );
										ImGui.EndDragDropSource();
									}
									if( !mUI.EditorWindow.EditingPreset && ImGui.BeginDragDropTarget() )
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
								if( !mUI.EditorWindow.EditingPreset && ImGui.GetDragDropPayload().NativePtr != null && ImGui.GetDragDropPayload().Data != IntPtr.Zero )
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
					ImGui.InputTextWithHint( "##JSONImportTextBox", "Paste a preset here and click \"Import\".", ref mPresetImportString, 1024 );   //Most exports max out around 500 characters with all waymarks, so this leaves heaps of room for a long name.
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
			WindowPos = ImGui.GetWindowPos();
			WindowSize = ImGui.GetWindowSize();

			//	We're done.
			ImGui.End();
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

		private readonly IntPtr mpLibraryPresetDragAndDropData;
	}
}