using ImGuiNET;
using ImGuiScene;
using System;
using System.Numerics;

namespace UIDev
{
	class UITest : IPluginUIMock
	{
		public static void Main( string[] args )
		{
			UIBootstrap.Inititalize( new UITest() );
		}

		private TextureWrap goatImage;
		private SimpleImGuiScene scene;

		public void Initialize( SimpleImGuiScene scene )
		{
			// scene is a little different from what you have access to in dalamud
			// but it can accomplish the same things, and is really only used for initial setup here

			// eg, to load an image resource for use with ImGui 
			this.goatImage = scene.LoadImage( @"goat.png" );

			scene.OnBuildUI += Draw;

			this.Visible = true;

			// saving this only so we can kill the test application by closing the window
			// (instead of just by hitting escape)
			this.scene = scene;
		}

		public void Dispose()
		{
			this.goatImage.Dispose();
		}

		// You COULD go all out here and make your UI generic and work on interfaces etc, and then
		// mock dependencies and conceivably use exactly the same class in this testbed and the actual plugin
		// That is, however, a bit excessive in general - it could easily be done for this sample, but I
		// don't want to imply that is easy or the best way to go usually, so it's not done here either
		private void Draw()
		{
			ImGui.ShowDemoWindow();

			DrawMainWindow();
			DrawSettingsWindow();

			if( !Visible )
			{
				this.scene.ShouldQuit = true;
			}
		}

		#region Nearly a copy/paste of PluginUI
		private bool visible = false;
		public bool Visible
		{
			get { return this.visible; }
			set { this.visible = value; }
		}

		private bool settingsVisible = false;
		public bool SettingsVisible
		{
			get { return this.settingsVisible; }
			set { this.settingsVisible = value; }
		}

		// this is where you'd have to start mocking objects if you really want to match
		// but for simple UI creation purposes, just hardcoding values works

		public void DrawMainWindow()
		{
			if( !Visible )
			{
				return;
			}

			ImGui.SetNextWindowSize( new Vector2( 250, 330 ), ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 250, 330 ), new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( "Waymark Library", ref visible, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysVerticalScrollbar ) )
			{
				bool dummyBool = false;
				ImGui.Checkbox( "Filter on current zone", ref dummyBool );
				/*ImGui.SameLine();
				if( ImGui.Button( "Settings" ) )
				{
					SettingsVisible = true;
				}*/
				if( ImGui.CollapsingHeader( "Liminal Space" ) )
				{
				}
				if( ImGui.CollapsingHeader( "The Gandof Thunder Plains" ) )
				{
					if( ImGui.Selectable( "PF Setup" ) )
					{
					}
					if( ImGui.Selectable( "PF" ) )
					{
					}
					if( ImGui.Selectable( "Super Twister Dodecahedron Strat", true ) )
					{
					}
					if( ImGui.Selectable( "Basic Uptime" ) )
					{
					}
				}
				if( ImGui.CollapsingHeader( "Ashfall" ) )
				{
				}
				if( ImGui.CollapsingHeader( "The Halo" ) )
				{
					if( ImGui.Selectable( "PF" ) )
					{
					}
					if( ImGui.Selectable( "Tank Invuln Strat" ) )
					{
					}
				}
				if( ImGui.CollapsingHeader( "Great Glacier" ) )
				{
				}
			}

			Vector2 windowpos = ImGui.GetWindowPos();
			Vector2 windowSize = ImGui.GetWindowSize();
			ImGui.End();
			ImGui.SetNextWindowSize( new Vector2( 375, 330 ), ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 250, 330 ), new Vector2( 250, windowSize.Y ) );
			ImGui.SetNextWindowPos( new Vector2( windowpos.X + windowSize.X, windowpos.Y ) );
			if( Visible && ImGui.Begin( "Preset Info", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize /*| ImGuiWindowFlags.NoTitleBar*/ ) )
			{
				ImGui.BeginGroup();
				ImGui.Text( "Copy to slot:" );
				ImGui.SameLine();
				ImGui.BeginGroup();
				if( ImGui.Button( "1" ) )
				{
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
				bool dummyBool = false;
				ImGui.Checkbox( "Use current zone.", ref dummyBool );
				ImGui.Text( "Preset Info:" );
				ImGui.Text( @"A:  099.94,  000.00,  081.81
B: 107.88, 000.00, 099.95 
C: 100.03, 000.00, 107.88
D: 115.83, 000.00, 110.59
1:  084.15, 000.00, 110.37
2:  109.24, 000.00, 094.46
3:  090.97, 000.00, 094.83
4:  118.09, 000.00, 099.91

Zone ID: 694( Liminal Space )
Last Modified: 2 / 25 / 2020 8:29:17 PM" );
			}

			ImGui.End();
		}

		public void DrawSettingsWindow()
		{
			if( !SettingsVisible )
			{
				return;
			}

			/*ImGui.SetNextWindowSize( new Vector2( 232, 75 ), ImGuiCond.Always );
			if( ImGui.Begin( "A Wonderful Configuration Window", ref this.settingsVisible,
				ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse ) )
			{
				if( ImGui.Checkbox( "Random Config Bool", ref this.fakeConfigBool ) )
				{
					// nothing to do in a fake ui!
				}
			}
			ImGui.End();*/
		}
		#endregion
	}
}
