using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Policy;

namespace WaymarkPresetPlugin
{
	// It is good to have this be disposable in general, in case you ever need it
	// to do any cleanup
	public class PluginUI : IDisposable
	{
		//	Construction
		public PluginUI( Configuration configuration, Dictionary<UInt16, string> zoneNames )
		{
			mConfiguration = configuration;
			ZoneNames = zoneNames;
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
			DrawSettingsWindow();
		}

		protected void DrawMainWindow()
		{
			//*****TODO: Add a filter to current zone checkbox to the list if we can find the zone that we are in.*****
			if( !MainWindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 375, 330 ), ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 330 ), new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( "Waymark Library", ref mMainWindowVisible, ImGuiWindowFlags.NoCollapse ) )
			{
				//*****TODO: Do the filtering.*****
				/*bool dummyBool = false;
				ImGui.Checkbox( "Filter on current zone", ref dummyBool );*/

				//	Just drop in the data.
				var dict = mConfiguration.PresetLibrary.GetSortedIndices();
				if( dict.Count > 0 )
				{
					foreach( KeyValuePair<UInt16, List<int>> zone in dict )
					{
						if( ImGui.CollapsingHeader( ZoneNames.ContainsKey( zone.Key ) ? ZoneNames[zone.Key].ToString() : "Unknown Zone" ) )
						{
							foreach( int index in zone.Value )
							{
								//	Unfortunately, ImGui seems to track selectables by the label string, so we will insert the index to make each one have a unique label.
								if( ImGui.Selectable( index.ToString() + ": " + mConfiguration.PresetLibrary.Presets[index].Name, index == SelectedPreset ) )
								{
									SelectedPreset = index;
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

			//	Store the position and size so that we can keep the companion info window in the right place.
			MainWindowPos = ImGui.GetWindowPos();
			MainWindowSize = ImGui.GetWindowSize();

			//	We're done.
			ImGui.End();
		}

		protected void DrawInfoWindow()
		{
			//*****TODO: Consider having a drawing of marks around barycenter?*****
			//*****TODO: Allow direct place only with filter to current zone active (If we can even find a function to direct place).*****
			//*****TODO: Add editor button/window.*****

			ImGui.SetNextWindowSize( new Vector2( 375, 330 ), ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 250, 330 ), new Vector2( 250, MainWindowSize.Y ) );
			ImGui.SetNextWindowPos( new Vector2( MainWindowPos.X + MainWindowSize.X, MainWindowPos.Y ) );
			if( MainWindowVisible && ImGui.Begin( "Preset Info", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize /*| ImGuiWindowFlags.NoTitleBar*/ ) )
			{
				if( SelectedPreset >= 0 && SelectedPreset < mConfiguration.PresetLibrary.Presets.Count )
				{
					ImGui.BeginGroup();
					ImGui.Text( "Copy to slot:" );
					ImGui.SameLine();
					ImGui.BeginGroup();
					if( ImGui.Button( "1" ) )
					{
						//*****TODO: Hook up these buttons!*****
					}
					ImGui.SameLine();
					if( ImGui.Button( "2" ) )
					{
					}
					ImGui.SameLine();
					if( ImGui.Button( "3" ) )
					{
					}
					ImGui.SameLine();
					if( ImGui.Button( "4" ) )
					{
					}
					ImGui.SameLine();
					if( ImGui.Button( "5" ) )
					{
					}
					ImGui.EndGroup();
					//*****TODO: 
					/*bool dummyBool = false;
					ImGui.Checkbox( "Use current zone.", ref dummyBool );*/
					ImGui.Text( "Preset Info:" );
					//*****TODO: At some point it would be nice to make this a monospaced font.*****
					ImGui.Text( mConfiguration.PresetLibrary.Presets[SelectedPreset].GetPresetDataString() );
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.Spacing();
					ImGui.PushStyleColor( ImGuiCol.Text, 0xee4444ff );
					if( ImGui.Button( "Delete Preset" ) )
					{
						mConfiguration.PresetLibrary.DeletePreset( SelectedPreset );
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

		protected void DrawSettingsWindow()
		{
			//*****TODO: Probably have an option to turn off zone categories and just do a straight list.*****
			//*****TODO: *If* it ever becomes possible to do direct placement, probably make it an option that people have to turn on.
			if( !SettingsWindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSize( new Vector2( 232, 75 ), ImGuiCond.Always );
			if( ImGui.Begin( "Waymark Settings", ref this.mSettingsWindowVisible,
				ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				ImGui.Text( "No settings currently." );
			}
			ImGui.End();
		}

		private Configuration mConfiguration;

		//	Need a real backing field on the following properties for use with ImGui.
		protected bool mMainWindowVisible = false;
		public bool MainWindowVisible
		{
			get { return this.mMainWindowVisible; }
			set { this.mMainWindowVisible = value; }
		}

		protected bool mSettingsWindowVisible = false;
		public bool SettingsWindowVisible
		{
			get { return this.mSettingsWindowVisible; }
			set { this.mSettingsWindowVisible = value; }
		}

		public Vector2 MainWindowPos { get; protected set; }
		public Vector2 MainWindowSize { get; protected set; }

		public int SelectedPreset { get; protected set; } = -1;
		public Dictionary<UInt16, string> ZoneNames { get; protected set; }
	}
}
