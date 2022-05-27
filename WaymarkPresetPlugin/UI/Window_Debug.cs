using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

using CheapLoc;

using Dalamud.Data;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Dalamud.Plugin;

using ImGuiNET;

using Lumina;
using Lumina.Data.Files;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace WaymarkPresetPlugin
{
	internal sealed class Window_Debug : IDisposable
	{
		public Window_Debug( PluginUI UI, DalamudPluginInterface pluginInterface, DataManager dataManager, ClientState clientState, GameGui gameGui )
		{
			mUI = UI;
			mPluginInterface = pluginInterface;
			mDataManager = dataManager;
			mClientState = clientState;
			mGameGui = gameGui;
		}

		public void Dispose()
		{

		}

		public unsafe void Draw()
		{
			if( !WindowVisible )
			{
				return;
			}

			List<Vector3> LGBItemCoords = new();

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
				ImGui.Text( "Drag and Drop Data:" );
				ImGui.Indent();
				ImGui.Text( $"Zone: 0x{mUI.mpLibraryZoneDragAndDropData:X}" );
				ImGui.Text( $"Preset: 0x{mUI.mpLibraryPresetDragAndDropData:X}" );
				ImGui.Text( $"Waymark: 0x{mUI.mpEditWaymarkDragAndDropData:X}" );
				ImGui.Text( $"Coords: 0x{mUI.mpEditWaymarkCoordDragAndDropData:X}" );
				ImGui.Spacing();
				if( ImGui.GetDragDropPayload().NativePtr != null )
				{
					ImGui.Text( $"Current Payload: 0x{ImGui.GetDragDropPayload().Data:X}" );
					ImGui.Text( $"Current Payload Contents: 0x{Marshal.ReadInt32( ImGui.GetDragDropPayload().Data )}" );
					
				}
				ImGui.Unindent();

				ImGui.Text( $"Current BG Folder: {mCurrentBGFolder}" );
				if( ImGui.BeginCombo( "Layer Type", mLayerSelection.ToString() ) )
				{
					foreach( Lumina.Data.Parsing.Layer.LayerEntryType entry in Enum.GetValues( typeof( Lumina.Data.Parsing.Layer.LayerEntryType ) ) )
					{
						if( ImGui.Selectable( entry.ToString(), mLayerSelection == entry ) ) mLayerSelection = entry;
						if( mLayerSelection == entry ) ImGui.SetItemDefaultFocus();
					}
					ImGui.EndCombo();
				}

				if( ImGui.Button( "Load LGB Data" ) )
				{
					LoadInstanceObjects( mClientState.TerritoryType, mLayerSelection );
				}

				
				foreach( var item in mMapRanges )
				{
					//var itemAsMapRange = (Lumina.Data.Parsing.Layer.LayerCommon.MapRangeInstanceObject)item.Object;
					ImGui.Text( $"X: {item.Transform.Translation.X}, Y: {item.Transform.Translation.Y}, Z: {item.Transform.Translation.Z}, Instance ID: {item.InstanceId}" );
					LGBItemCoords.Add( new( item.Transform.Translation.X, item.Transform.Translation.Y, item.Transform.Translation.Z ) );
				}

				if( ImGui.Button( "Load LCB Data" ) )
				{
					LoadLCB( mClientState.TerritoryType );
				}

				if( mCurrentLCBFile != null )
				{
					foreach( var item in mCurrentLCBFile.mRecords )
					{
						ImGui.Text( $"{item.Min}, {item.Max}" );
					}

					DrawLCBLinesInWorld();
				}
			}

			//	We're done.
			ImGui.End();

			DrawObjectsInWorld( LGBItemCoords );
		}

		private void DrawObjectsInWorld( List<Vector3> points )
		{
			ImGui.SetNextWindowPos( ImGui.GetMainViewport().Pos );
			ImGui.SetNextWindowSize( ImGui.GetMainViewport().Size );
			if( ImGui.Begin( "###LGBObjectOverlayWindow", ImGuiUtils.OverlayWindowFlags ) )
			{
				foreach( var point in points )
				{
					float scale = 1f;
					float distMin = 1;
					float distMax = 50;
					float distNorm = 1;
					float scaleMin = 5f;
					float scaleMax = 50f;
					float distance = mClientState.LocalPlayer != null ? ( point - mClientState.LocalPlayer.Position ).Length() : 0;
					//distance = Math.Max( distMin, Math.Min( distance, distMax ) );
					if( distance <= distMin ) scale = scaleMax;
					else if( distance >= distMax ) scale = scaleMin;
					else
					{
						distNorm = distance / ( distMax - distMin );
						scale = scaleMax * ( 1f - distNorm );
					}
					mGameGui.WorldToScreen( point, out Vector2 screenPos );
					ImGui.GetWindowDrawList().AddCircle( screenPos, scale, 0xFF0000FF );
				}
			}

			ImGui.End();
		}

		private void DrawLCBLinesInWorld()
		{
			ImGui.SetNextWindowPos( ImGui.GetMainViewport().Pos );
			ImGui.SetNextWindowSize( ImGui.GetMainViewport().Size );
			if( ImGui.Begin( "###LCBObjectOverlayWindow", ImGuiUtils.OverlayWindowFlags ) )
			{
				if( mCurrentLCBFile != null )
				{
					foreach( var entry in mCurrentLCBFile.mRecords )
					{

						mGameGui.WorldToScreen( entry.Min, out Vector2 screenPos1 );
						mGameGui.WorldToScreen( entry.Max, out Vector2 screenPos2 );
						ImGui.GetWindowDrawList().AddLine( screenPos1, screenPos2, 0xFF0000FF, 3 );
					}
				}
			}

			ImGui.End();
		}

		private void LoadInstanceObjects( UInt16 territoryType, Lumina.Data.Parsing.Layer.LayerEntryType entryType )
		{
			//PluginLog.LogDebug( $"Debug Window: Entered TerritoryType {territoryType}." );
			if( mClientState.TerritoryType > 0 )
			{
				mMapRanges.Clear();
				ExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType> territorySheet = mDataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>();
				var territoryRow = territorySheet.GetRow( territoryType );

				mCurrentBGFolder = $"bg/{territoryRow.Bg}";
				mCurrentBGFolder = mCurrentBGFolder[..mCurrentBGFolder.LastIndexOf( '/' )];
				foreach( var fileName in LBGFileNames )
				{
					string filePath = $"{mCurrentBGFolder}/{fileName}";

					//PluginLog.LogDebug( $"Debug Window: Getting File {filePath}." );
					var file = mDataManager.GetFile<LgbFile>( filePath );
					if( file != null )
					{
						//PluginLog.LogDebug( $"Debug Window: Non-null LGB file." );
						foreach( var layer in file.Layers )
						{
							//PluginLog.LogDebug( $"Debug Window: Saw Layer {layer.Name}." );
							//if( layer.Name.Contains( "LVD_maprange", StringComparison.OrdinalIgnoreCase ) )
							//{
							//PluginLog.LogDebug( $"Debug Window: Processing Layer {layer.Name}." );
							foreach( var item in layer.InstanceObjects )
							{
								if( item.AssetType == entryType )
								{
									mMapRanges.Add( item );
								}
							}
							//}
						}
					}
				}
			}
		}

		private void LoadLCB( UInt16 territoryType )
		{
			if( mClientState.TerritoryType > 0 )
			{
				mMapRanges.Clear();
				ExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType> territorySheet = mDataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>();
				var territoryRow = territorySheet.GetRow( territoryType );

				mCurrentBGFolder = $"bg/{territoryRow.Bg}";
				string areaName = mCurrentBGFolder[( mCurrentBGFolder.LastIndexOf( '/' ) + 1 )..];
				mCurrentBGFolder = mCurrentBGFolder[..mCurrentBGFolder.LastIndexOf( '/' )];
				string filePath = $"{mCurrentBGFolder}/{areaName}.lcb";
				var file = mDataManager.GetFile( filePath );
				if( file != null )
				{
					mCurrentLCBFile = new( file.Data );
				}
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
		private readonly ClientState mClientState;
		private readonly GameGui mGameGui;

		private LCBFile mCurrentLCBFile;
		private readonly List<Lumina.Data.Parsing.Layer.LayerCommon.InstanceObject> mMapRanges = new();
		private Lumina.Data.Parsing.Layer.LayerEntryType mLayerSelection = Lumina.Data.Parsing.Layer.LayerEntryType.AssetNone;
		private string mCurrentBGFolder;
		private readonly string[] LBGFileNames =
		{
			"bg.lgb",
			"planevent.lgb",
			"planlive.lgb",
			"planmap.lgb",
			"sound.lgb",
			"vfx.lgb",
		};

		internal class LCBFile
		{
			public unsafe LCBFile( byte[] data )
			{
				fixed( byte* pData = data )
				{
					mHeader = Marshal.PtrToStructure<Header>( new( pData ) );
					for( int i = 0; i < mHeader.RecordCount; ++i )
					{
						mRecords.Add( Marshal.PtrToStructure<Record>( new( pData + i * sizeof( Record ) ) ) );
					}
				}
			}

			[StructLayout(LayoutKind.Sequential)]
			public unsafe struct Header
			{
				public UInt32 MagicNumber1;
				public UInt32 FileLength_Bytes;
				public UInt32 MagicNumber2;
				public fixed byte Unknown[12];
				public UInt32 RecordCount;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct Record
			{
				public UInt32 Unknown1;
				public UInt32 Unknown2;
				public Vector3 Min;
				public Vector3 Max;
			}

			public readonly Header mHeader;
			public readonly List<Record> mRecords = new();
		}

		/*[StructLayout( LayoutKind.Sequential )]
		internal struct InstanceObjectHeader
		{
			public uint Type;
			public uint Unknown;
			public uint NameOffset;
			public Vector3 Translation;
			public Vector3 Rotation;
			public Vector3 Scale;
		}

		[StructLayout(LayoutKind.Sequential)]
		internal unsafe struct MapRangeEntry
		{
			public InstanceObjectHeader Header;
			public uint Type;
			public ushort Unknown;
			public ushort Unknown2;
			public fixed byte Unknown3[16];
			public fixed byte Unknown4[36];
		}*/
	}
}
