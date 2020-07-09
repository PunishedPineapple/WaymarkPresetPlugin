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
			//Title bar
			//list/tree view
			//header list if configured
			//list of headers or sub-headers under zone headers with an item per preset, buttons to copy to slot (1,2,3,4,5 buttons).
			//expanding preset lists coords and other information.  Consider having a drawing of marks around barycenter?
			//*****TODO: Allow direct place only with filter to current zone active (If we can even find a function to direct place).*****
			if( !MainWindowVisible )
			{
				return;
			}

			ImGui.SetNextWindowSize( new Vector2( 375, 330 ), ImGuiCond.FirstUseEver );

			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 330 ), new Vector2( float.MaxValue, float.MaxValue ) );

			if( ImGui.Begin( "Waymark Library", ref mMainWindowVisible, ImGuiWindowFlags.NoCollapse ) )
			{
				//*****TODO: Do the filtering.*****
				/*bool dummyBool = false;
				ImGui.Checkbox( "Filter on current zone", ref dummyBool );*/

				//	Just drop in the data.
				var dict = mConfiguration.PresetLibrary.GetSortedIndices();
				foreach( KeyValuePair<UInt16, List<int>> zone in dict )
				{
					if( ImGui.CollapsingHeader( ZoneNames.ContainsKey( zone.Key ) ? ZoneNames[zone.Key].ToString() : "Unknown Zone" ) )
					{
						foreach( int index in zone.Value )
						{
							//*****TODO: How do we handle showing the item as selected?*****
							ImGui.Selectable( mConfiguration.PresetLibrary.Presets[index].Name );
						}
					}
				}
			}

			ImGui.End();
		}

		protected void DrawInfoWindow()
		{

		}

		protected void DrawSettingsWindow()
		{
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

		public Dictionary<UInt16, string> ZoneNames { get; protected set; }
	}
}
