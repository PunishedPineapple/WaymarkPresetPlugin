using System;
using System.Numerics;

using CheapLoc;

using Dalamud.Interface;
using Dalamud.Logging;
using Dalamud.Plugin;

using ImGuiNET;

namespace WaymarkPresetPlugin
{
	internal sealed class Window_InfoPane : IDisposable
	{
		public Window_InfoPane( PluginUI UI, DalamudPluginInterface pluginInterface, Configuration configuration )
		{
			mUI = UI;
			mPluginInterface = pluginInterface;
			mConfiguration = configuration;
		}

		public void Dispose()
		{

		}

		public void Draw()
		{
			if( !mUI.LibraryWindow.WindowVisible || ( mUI.LibraryWindow.SelectedPreset < 0 && !mConfiguration.AlwaysShowInfoPane ) )
			{
				return;
			}

			DrawInfoWindowLayoutPass();

			ImGui.SetNextWindowSize( mWindowSize );
			ImGui.SetNextWindowPos( new Vector2( mUI.LibraryWindow.WindowPos.X + mUI.LibraryWindow.WindowSize.X, mUI.LibraryWindow.WindowPos.Y ) );   //Note that this does *not* need to be viewport-relative, since it is just an offset relative to the library window.
			if( ImGui.Begin( Loc.Localize( "Window Title: Preset Info", "Preset Info" ) + "###Preset Info", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar ) )
			{
				if( mUI.LibraryWindow.SelectedPreset >= 0 && mUI.LibraryWindow.SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
				{
					ImGui.BeginGroup();
					ImGui.Text( Loc.Localize( "Info Pane Text: Copy to Slot Label", "Copy to slot:" ) );
					ImGui.SameLine();
					ImGui.BeginGroup();
					if( ImGui.Button( "1" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset], 1u );
					}
					ImGui.SameLine();
					if( ImGui.Button( "2" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset], 2u );
					}
					ImGui.SameLine();
					if( ImGui.Button( "3" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset], 3u );
					}
					ImGui.SameLine();
					if( ImGui.Button( "4" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset], 4u );
					}
					ImGui.SameLine();
					if( ImGui.Button( "5" ) )
					{
						CopyPresetToGameSlot( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset], 5u );
					}
					float rightAlignPos;
					string placeButtonText = Loc.Localize( "Button: Place", "Place" );
					ImGui.SameLine();
					if( MemoryHandler.FoundDirectPlacementSigs() )
					{
						if( ImGui.Button( placeButtonText + "###Place" ) )
						{
							MemoryHandler.PlacePreset( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].GetAsGamePreset() /*, mConfiguration.AllowClientSidePlacementInOverworldZones*/ );
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
					mWindowSize.X = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X;
					rightAlignPos = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X;


					ImGui.EndGroup();
					ImGui.Text( Loc.Localize( "Info Pane Text: Preset Info Label", "Preset Info:" ) );
					string mapViewButtonText = Loc.Localize( "Button: Map View", "Map View" );
					ImGui.SameLine( rightAlignPos - ImGui.CalcTextSize( mapViewButtonText ).X - ImGui.GetStyle().WindowPadding.X - ImGui.GetStyle().FramePadding.X * 2 );
					if( ImGui.Button( mapViewButtonText + "###Map View Button" ) )
					{
						mUI.MapWindow.WindowVisible = !mUI.MapWindow.WindowVisible;
					}

					if( ImGui.BeginTable( "###PresetInfoPaneWaymarkDataTable", 4 ) )
					{
						ImGui.TableSetupColumn( Loc.Localize( "Info Pane Text: Waymark Column Header", "Waymark" ) + "###Waymark", ImGuiTableColumnFlags.WidthFixed, 15 * ImGui.GetIO().FontGlobalScale );
						ImGui.TableSetupColumn( "X", ImGuiTableColumnFlags.WidthStretch );
						ImGui.TableSetupColumn( "Y", ImGuiTableColumnFlags.WidthStretch );
						ImGui.TableSetupColumn( "Z", ImGuiTableColumnFlags.WidthStretch );
						for( int i = 0; i < 8; ++i )
						{
							var waymark = mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset][i];
							ImGui.TableNextRow();
							ImGui.TableSetColumnIndex( 0 );
							ImGui.Text( $"{mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].GetNameForWaymarkIndex( i )}:" );
							ImGui.TableSetColumnIndex( 1 );
							ImGuiUtils.RightAlignTableText( waymark.Active ? waymark.X.ToString( "0.00" ) : Loc.Localize( "Info Pane Text: Unused Waymark", "Unused" ) );
							ImGui.TableSetColumnIndex( 2 );
							ImGuiUtils.RightAlignTableText( waymark.Active ? waymark.Y.ToString( "0.00" ) : " " );
							ImGui.TableSetColumnIndex( 3 );
							ImGuiUtils.RightAlignTableText( waymark.Active ? waymark.Z.ToString( "0.00" ) : " " );
						}
						ImGui.EndTable();
					}

					string zoneStr = ZoneInfoHandler.GetZoneInfoFromContentFinderID( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].MapID ).DutyName;
					zoneStr += mConfiguration.ShowIDNumberNextToZoneNames ? $" ({mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].MapID})" : "";
					ImGui.Text( String.Format( Loc.Localize( "Info Pane Text: Zone Label", "Zone: {0}" ), zoneStr ) );
					ImGui.Text( String.Format( Loc.Localize( "Info Pane Text: Last Modified Label", "Last Modified: {0}" ), mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset].Time.LocalDateTime ) );

					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					if( ImGui.Button( Loc.Localize( "Button: Export to Clipboard", "Export to Clipboard" ) + "###Export to Clipboard" ) )
					{
						if( mUI.LibraryWindow.SelectedPreset >= 0 && mUI.LibraryWindow.SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
						{
							Win32Clipboard.CopyTextToClipboard( WaymarkPresetExport.GetExportString( mConfiguration.PresetLibrary.Presets[mUI.LibraryWindow.SelectedPreset] ) );
						}
					}
					ImGui.SameLine();
					if( ImGui.Button( Loc.Localize( "Button: Edit", "Edit" ) + "###Edit" ) && !mUI.EditorWindow.EditingPreset )  //Don't want to let people start editing while the edit window is already open.
					{
						mUI.EditorWindow.TryBeginEditing( mUI.LibraryWindow.SelectedPreset );
					}
					ImGui.SameLine();
					ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
					if( ImGui.Button( Loc.Localize( "Button: Delete", "Delete" ) + "###Delete" ) && !mUI.EditorWindow.EditingPreset )
					{
						WantToDeleteSelectedPreset = true;
					}
					mWindowSize.X = Math.Max( mWindowSize.X, ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X );
					if( WantToDeleteSelectedPreset )
					{
						ImGui.Text( Loc.Localize( "Info Pane Text: Confirm Delete Label", "Confirm delete: " ) );
						ImGui.SameLine();
						if( ImGui.Button( Loc.Localize( "Button: Yes", "Yes" ) + "###Yes Button" ) )
						{
							mConfiguration.PresetLibrary.DeletePreset( mUI.LibraryWindow.SelectedPreset );
							WantToDeleteSelectedPreset = false;
							if( mUI.LibraryWindow.SelectedPreset == mUI.EditorWindow.EditingPresetIndex ) mUI.EditorWindow.CancelEditing();
							mUI.LibraryWindow.TryDeselectPreset();
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
					mWindowSize.Y = ImGui.GetItemRectMax().Y - ImGui.GetWindowPos().Y + ImGui.GetStyle().WindowPadding.Y;
				}
				else
				{
					ImGui.Text( Loc.Localize( "Info Pane Text: No Preset Selected", "No preset selected." ) );
				}
			}

			ImGui.End();
		}

		private void DrawInfoWindowLayoutPass()
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
				mWindowSize.X = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X;
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
				mWindowSize.X = Math.Max( mWindowSize.X, ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + ImGui.GetStyle().WindowPadding.X );
				if( WantToDeleteSelectedPreset )
				{
					ImGui.Button( "Don't do it!" );
				}
				mWindowSize.Y = ImGui.GetItemRectMax().Y - ImGui.GetWindowPos().Y + ImGui.GetStyle().WindowPadding.Y;
			}

			ImGui.PopStyleVar();

			ImGui.End();
		}
		private void CopyPresetToGameSlot( WaymarkPreset preset, uint slot )
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

		public void CancelPendingDelete()
		{
			WantToDeleteSelectedPreset = false;
		}

		private Vector2 mWindowSize;

		private readonly PluginUI mUI;
		private readonly DalamudPluginInterface mPluginInterface;
		private readonly Configuration mConfiguration;

		public bool WantToDeleteSelectedPreset { get; private set; } = false;
	}
}
