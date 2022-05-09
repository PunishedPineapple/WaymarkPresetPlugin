using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Numerics;
using ImGuiNET;
using ImGuiScene;
using Dalamud.Plugin;
using CheapLoc;

namespace WaymarkPresetPlugin
{
	internal sealed class Window_Debug : IDisposable
	{
		public Window_Debug( PluginUI UI, DalamudPluginInterface pluginInterface )
		{
			mUI = UI;
			mPluginInterface = pluginInterface;
		}

		public void Dispose()
		{

		}

		public void Draw()
		{
			if( !WindowVisible )
			{
				return;
			}

			//	Draw the window.
			ImGui.SetNextWindowSize( new Vector2( 375, 340 ) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver );
			ImGui.SetNextWindowSizeConstraints( new Vector2( 375, 340 ) * ImGui.GetIO().FontGlobalScale, new Vector2( float.MaxValue, float.MaxValue ) );
			if( ImGui.Begin( Loc.Localize( "Window Title: Debug Tools", "Debug Tools" ) + "###Debug Tools", ref mWindowVisible ) )
			{
				if( ImGui.Button( "Export Localizable Strings" ) )
				{
					string pwd = Directory.GetCurrentDirectory();
					Directory.SetCurrentDirectory( mPluginInterface.AssemblyLocation.DirectoryName );
					Loc.ExportLocalizable();
					Directory.SetCurrentDirectory( pwd );
				}
			}

			//	We're done.
			ImGui.End();
		}

		private bool mWindowVisible = false;
		public bool WindowVisible
		{
			get { return mWindowVisible; }
			set { mWindowVisible = value; }
		}

		private readonly PluginUI mUI;
		private readonly DalamudPluginInterface mPluginInterface;
	}
}
