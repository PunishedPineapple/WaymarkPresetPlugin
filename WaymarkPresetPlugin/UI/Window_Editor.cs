using System;
using System.Numerics;
using System.Runtime.InteropServices;

using CheapLoc;

using Dalamud.Interface;
using Dalamud.Plugin;

using ImGuiNET;

namespace WaymarkPresetPlugin
{
	internal sealed class Window_Editor : IDisposable
	{
		public Window_Editor( PluginUI UI, DalamudPluginInterface pluginInterface, Configuration configuration, IntPtr pEditWaymarkDragAndDropData )
		{
			mUI = UI;
			mPluginInterface = pluginInterface;
			mConfiguration = configuration;
			mpEditWaymarkDragAndDropData = pEditWaymarkDragAndDropData;
		}

		public void Dispose()
		{

		}

		public void Draw()
		{
			if( EditingPresetIndex < 0 || EditingPresetIndex >= mConfiguration.PresetLibrary.Presets.Count )
			{
				return;
			}

			if( ImGui.Begin( Loc.Localize( "Window Title: Preset Editor", "Preset Editor" ) + "###Preset Editor", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize ) )
			{
				ImGuiUtils.TitleBarHelpButton( () => { mUI.HelpWindow.OpenHelpWindow( HelpWindowPage.Editing ); }, 0, UiBuilder.IconFont );

				//ImGui.PushTextWrapPos();
				//ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
				//ImGui.TextWrapped( "SE has banned people for having markers that are outside of normally placeable areas.  Please use the editor responsibly and avoid waymarks outside of walkable areas, or above or below the ground." );
				//ImGui.PopStyleColor();
				//ImGui.PopTextWrapPos();

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
							ImGui.Checkbox( $"{waymark.Label}             ###{waymark.Label}", ref waymark.Active );    //Padded text to make more area to grab the waymark for drag and drop.
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
					ImGui.Text( Loc.Localize( "Edit Window Text: Zone Dropdown Label", "Zone: " ) );
					if( ImGui.BeginCombo( "##MapID", mConfiguration.GetZoneName( ScratchEditingPreset.MapID ) ) )
					{
						ImGui.Text( Loc.Localize( "Edit Window Text: Zone Search Label", "Search: " ) );
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
					CancelEditing();
				}
				ImGui.SameLine( ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize( "Map View" ).X - ImGui.GetStyle().FramePadding.X * 2 );
				if( ImGui.Button( Loc.Localize( "Button: Map View", "Map View" ) + "###Map View Button" ) )
				{
					mUI.MapWindow.WindowVisible = !mUI.MapWindow.WindowVisible;
				}
			}
			ImGui.End();
		}

		public void TryBeginEditing( int presetIndex )
		{
			if( presetIndex < 0 || presetIndex >= mConfiguration.PresetLibrary.Presets.Count ) return;

			EditingPresetIndex = presetIndex;
			ScratchEditingPreset = new ScratchPreset( mConfiguration.PresetLibrary.Presets[mUI.EditorWindow.EditingPresetIndex] );
		}

		public void CancelEditing()
		{
			EditingPresetIndex = -1;
			ScratchEditingPreset = null;
		}

		private readonly PluginUI mUI;
		private readonly DalamudPluginInterface mPluginInterface;
		private readonly Configuration mConfiguration;

		private readonly IntPtr mpEditWaymarkDragAndDropData;

		private ZoneSearcher EditWindowZoneSearcher { get; set; } = new ZoneSearcher();
		private string mEditWindowZoneFilterString = "";
		private bool EditWindowZoneComboWasOpen { get; set; } = false;
		public int EditingPresetIndex { get; private set; } = -1;
		internal bool EditingPreset => EditingPresetIndex != -1;
		internal ScratchPreset ScratchEditingPreset { get; private set; }
	}
}
