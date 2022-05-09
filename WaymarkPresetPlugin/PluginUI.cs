using System;
using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Plugin;

using ImGuiScene;

namespace WaymarkPresetPlugin
{
	// It is good to have this be disposable in general, in case you ever need it
	// to do any cleanup
	public sealed class PluginUI : IDisposable
	{
		public PluginUI( Configuration configuration, DalamudPluginInterface pluginInterface, DataManager dataManager, GameGui gameGui, ClientState clientState )
		{

			//	Allocate drag and drop memory.
			mpLibraryPresetDragAndDropData = Marshal.AllocHGlobal( sizeof( int ) );
			mpEditWaymarkDragAndDropData = Marshal.AllocHGlobal( sizeof( int ) );
			mpEditWaymarkCoordDragAndDropData = Marshal.AllocHGlobal( Marshal.SizeOf<Vector3>() );
			if( mpLibraryPresetDragAndDropData == IntPtr.Zero ||
				mpEditWaymarkDragAndDropData == IntPtr.Zero ||
				mpEditWaymarkCoordDragAndDropData  == IntPtr.Zero )
			{
				throw new Exception( "Error in PluginUI constructor: Unable to allocate memory for drag and drop info." );
			}

			//	Load waymark icons.
			WaymarkIconTextures[0] ??= dataManager.GetImGuiTextureIcon( 61241 );   //A
			WaymarkIconTextures[1] ??= dataManager.GetImGuiTextureIcon( 61242 );   //B
			WaymarkIconTextures[2] ??= dataManager.GetImGuiTextureIcon( 61243 );   //C
			WaymarkIconTextures[3] ??= dataManager.GetImGuiTextureIcon( 61247 );   //D
			WaymarkIconTextures[4] ??= dataManager.GetImGuiTextureIcon( 61244 );   //1
			WaymarkIconTextures[5] ??= dataManager.GetImGuiTextureIcon( 61245 );   //2
			WaymarkIconTextures[6] ??= dataManager.GetImGuiTextureIcon( 61246 );   //3
			WaymarkIconTextures[7] ??= dataManager.GetImGuiTextureIcon( 61248 );   //4

			//	Make child windows.
			LibraryWindow = new( this, pluginInterface, gameGui, configuration, clientState, mpLibraryPresetDragAndDropData );
			InfoPaneWindow = new( this, pluginInterface, configuration );
			MapWindow = new( this, pluginInterface, dataManager, configuration );
			HelpWindow = new( this, pluginInterface, configuration, mpEditWaymarkCoordDragAndDropData );
			DebugWindow = new( this, pluginInterface );
			SettingsWindow = new( this, pluginInterface, configuration );
			EditorWindow = new( this, pluginInterface, configuration, mpEditWaymarkDragAndDropData );
		}

		public void Dispose()
		{
			//	Clean up child windows.
			LibraryWindow?.Dispose();
			InfoPaneWindow?.Dispose();
			HelpWindow?.Dispose();
			MapWindow?.Dispose();
			DebugWindow?.Dispose();
			SettingsWindow?.Dispose();
			EditorWindow?.Dispose();

			//	Clean up any other textures.
			for( int i = 0; i < WaymarkIconTextures.Length; ++i )
			{
				WaymarkIconTextures[i]?.Dispose();
			}

			//	Free the drag and drop data.
			Marshal.FreeHGlobal( mpLibraryPresetDragAndDropData );
			Marshal.FreeHGlobal( mpEditWaymarkDragAndDropData );
			Marshal.FreeHGlobal( mpEditWaymarkCoordDragAndDropData );
		}

		public void Draw()
		{
			//	Draw the sub-windows.
			LibraryWindow.Draw();
			InfoPaneWindow.Draw();
			MapWindow.Draw();
			EditorWindow.Draw();
			SettingsWindow.Draw();
			HelpWindow.Draw();
			DebugWindow.Draw();
		}

		//	Child Windows
		internal readonly Window_Library LibraryWindow;
		internal readonly Window_InfoPane InfoPaneWindow;
		internal readonly Window_Map MapWindow;
		internal readonly Window_Help HelpWindow;
		internal readonly Window_Debug DebugWindow;
		internal readonly Window_Settings SettingsWindow;
		internal readonly Window_Editor EditorWindow;

		//	The fields below are here because multiple windows might need them.
		internal readonly TextureWrap[] WaymarkIconTextures = new TextureWrap[8];
		
		private readonly IntPtr mpLibraryPresetDragAndDropData;
		private readonly IntPtr mpEditWaymarkDragAndDropData;
		private readonly IntPtr mpEditWaymarkCoordDragAndDropData;
	}
}
