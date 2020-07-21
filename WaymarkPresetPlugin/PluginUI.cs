using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Security.Policy;
using Newtonsoft.Json;
using System.Diagnostics.Eventing.Reader;
using Lumina.Excel.GeneratedSheets;
using System.Linq;
using Dalamud.Plugin;

namespace WaymarkPresetPlugin
{
	// It is good to have this be disposable in general, in case you ever need it
	// to do any cleanup
	public class PluginUI : IDisposable
	{
		//	Construction
		public PluginUI( Configuration configuration, Dictionary<UInt16, Tuple<string, string>> zoneNames, MemoryHandler gameMemoryHandler )
		{
			mConfiguration = configuration;
			ZoneNames = zoneNames;
			mGameMemoryHandler = gameMemoryHandler;
		}

		//	Destruction
		public void Dispose()
		{
		}

		public void Draw()
		{
			//	Draw the sub-windows.
			DrawMainWindow();
			DrawInfoWindow();
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
			ImGui.SetNextWindowSize( new Vector2( 375, 330 ), ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 330 ), new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( "Waymark Library", ref mMainWindowVisible, ImGuiWindowFlags.NoCollapse ) )
			{
				//	Populate the preset list
				ImGui.BeginGroup();
				if( mConfiguration.mSortPresetsByZone )
				{
					var dict = mConfiguration.PresetLibrary.GetSortedIndices();
					if( dict.Count > 0 )
					{
						foreach( KeyValuePair<UInt16, List<int>> zone in dict )
						{
							if( ImGui.CollapsingHeader( ZoneNames.ContainsKey( zone.Key ) ? mConfiguration.mShowDutyNames ? ZoneNames[zone.Key].Item1.ToString() : ZoneNames[zone.Key].Item2.ToString() : "Unknown Zone" ) )
							{
								foreach( int index in zone.Value )
								{
									if( ImGui.Selectable( $"{mConfiguration.PresetLibrary.Presets[index].Name}###_{index.ToString()}", index == SelectedPreset ) )
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
					else
					{
						ImGui.Text( "Preset library empty!" );
					}
				}
				else
				{
					if( mConfiguration.PresetLibrary.Presets.Count > 0 )
					{
						if( ImGui.CollapsingHeader( "Presets" ) )
						{
							for( int i = 0; i < mConfiguration.PresetLibrary.Presets.Count; ++i )
							{
								if( ImGui.Selectable( $"{mConfiguration.PresetLibrary.Presets[i].Name}###_{i.ToString()}", i == SelectedPreset ) )
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
					else
					{
						ImGui.Text( "Preset library empty!" );
					}
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
					ImGui.BeginGroup();	//Buttons don't seem to work under a header without being in a group.
					ImGui.InputText( "", ref mPresetImportString, 1024 );   //Most exports max out around 500 characters with all waymarks, so this leaves heaps of room for a long name.
					if( ImGui.Button( "Import JSON" ) )
					{
						if( mConfiguration.PresetLibrary.ImportPreset( PresetImportString ) >= 0 )
						{
							PresetImportString = "";
							mConfiguration.Save();
						}
					}
					if( mGameMemoryHandler.FoundAllSigs() )
					{
						ImGui.SameLine();
						ImGui.Text( " or slot " );
						ImGui.SameLine();
						if( ImGui.Button( "1" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 1 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
						ImGui.SameLine();
						if( ImGui.Button( "2" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 2 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
						ImGui.SameLine();
						if( ImGui.Button( "3" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 3 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
						ImGui.SameLine();
						if( ImGui.Button( "4" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 4 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
						}
						ImGui.SameLine();
						if( ImGui.Button( "5" ) )
						{
							if( mConfiguration.PresetLibrary.ImportPreset( mGameMemoryHandler.ReadSlot( 5 ) ) >= 0 )
							{
								mConfiguration.Save();
							}
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
			if( ImGui.Begin( "Preset Info", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize /*| ImGuiWindowFlags.NoTitleBar*/ ) )
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
					ImGui.EndGroup();
					ImGui.Text( "Preset Info:" );
					ImGui.Text( mConfiguration.PresetLibrary.Presets[SelectedPreset].GetPresetDataString() );
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					if( ImGui.Button( "Export to Clipboard" ) )
					{
						if( SelectedPreset >= 0 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
						{
							Clipboard.SetText( JsonConvert.SerializeObject( mConfiguration.PresetLibrary.Presets[SelectedPreset] ) );
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
					ImGui.InputText( "###PresetName", ref ScratchEditingPreset.Name, 128 );
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
						ImGui.InputFloat( $"###{waymark.Label}-X", ref waymark.X );
					}
					ImGui.NextColumn();
					ImGui.Text( "Y" );
					foreach( var waymark in ScratchEditingPreset.Waymarks )
					{
						ImGui.InputFloat( $"###{waymark.Label}-Y", ref waymark.Y );
					}
					ImGui.NextColumn();
					ImGui.Text( "Z" );
					foreach( var waymark in ScratchEditingPreset.Waymarks )
					{
						ImGui.InputFloat( $"###{waymark.Label}-Z", ref waymark.Z );
					}
					ImGui.Columns( 1 );
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Text( "Zone: " );
					if( ImGui.BeginCombo( "###MapID", ZoneNames.ContainsKey( ScratchEditingPreset.MapID ) ? mConfiguration.mShowDutyNames ? ZoneNames[ScratchEditingPreset.MapID].Item1.ToString() : ZoneNames[ScratchEditingPreset.MapID].Item2.ToString() : "Unknown Zone" ) )
					{
						foreach( var zone in ZoneNames )
						{
							if( zone.Key != 0 && ImGui.Selectable( mConfiguration.mShowDutyNames ? ZoneNames[zone.Key].Item1.ToString() : ZoneNames[zone.Key].Item2.ToString(), zone.Key == ScratchEditingPreset.MapID ) )
							{
								ScratchEditingPreset.MapID = zone.Key;
							}

							if( zone.Key == ScratchEditingPreset.MapID )
							{
								ImGui.SetItemDefaultFocus();
							}
						}
						ImGui.EndCombo();
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
			}
			ImGui.End();
		}

		protected void DrawSettingsWindow()
		{
			if( !SettingsWindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSize( new Vector2( 300, 180 ) );
			if( ImGui.Begin( "Waymark Settings", ref mSettingsWindowVisible,
				ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				ImGui.Checkbox( "Always show preset info pane.", ref mConfiguration.mAlwaysShowInfoPane );
				ImGui.Checkbox( "Clicking the selected preset unselects it.", ref mConfiguration.mAllowUnselectPreset );
				ImGui.Checkbox( "Categorize presets by zone.", ref mConfiguration.mSortPresetsByZone );
				ImGui.Checkbox( "Show duty names instead of zone names.", ref mConfiguration.mShowDutyNames );
				ImGui.Spacing();
				if( ImGui.Button( "Save and Close" ) )
				{
					mConfiguration.Save();
					SettingsWindowVisible = false;
				}
			}
			ImGui.End();
		}

		protected void CopyPresetToGameSlot( WaymarkPreset preset, uint slot )
		{
			if( slot >= 1 && slot <= 5 )
			{
				byte[] gamePresetData = preset.ConstructGamePreset();
				if( gamePresetData.Length == 104 )
				{
					try
					{
						mGameMemoryHandler.WriteSlot( slot, gamePresetData );
					}
					catch( Exception e )
					{
						PluginLog.Log( $"Error while copying preset data to game slot: {e}" );
					}
				}
			}
		}

		protected Configuration mConfiguration;
		protected MemoryHandler mGameMemoryHandler;

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
		protected  ScratchPreset ScratchEditingPreset { get; set; }
		public Dictionary<UInt16, Tuple<string, string>> ZoneNames { get; protected set; }
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
