using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

using ImGuiNET;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Data;
using FFXIVClientStructs.FFXIV.Component.GUI;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace WaymarkPresetPlugin
{
	public class MapTestWindow
	{
		public MapTestWindow( ClientState clientState, GameGui gameGui, DataManager dataManager )
		{
			mClientState = clientState;
			mGameGui = gameGui;


			ExcelSheet<Lumina.Excel.GeneratedSheets.Map> mapSheet = dataManager.GetExcelSheet<Lumina.Excel.GeneratedSheets.Map>();
			foreach( var item in mapSheet )
			{
				MapDict.TryAdd( item.Id.ToString(), Tuple.Create( item.SizeFactor, item.OffsetX, item.OffsetY ) );
			}
		}

		public void Draw()
		{
			if( !ShowWindow )
			{
				return;
			}

			if( ImGui.Begin( "Map Coords Debug", ref ShowWindow,
				ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse ) )
			{
				var mapID = GetMapID();
				if( MapDict.ContainsKey( mapID ) )
				{
					mSizeFactor = MapDict[mapID].Item1;
					mOffset.X = MapDict[mapID].Item2;
					mOffset.Y = MapDict[mapID].Item3;
				}
				ImGui.Text( $"Map ID: {mapID}" );
				ImGui.Text( $"Size Factor: {mSizeFactor}" );
				ImGui.Text( $"Offset: X: {mOffset.X}, Y: {mOffset.Y}" );
				/*ImGui.InputFloat2( "Map Offset", ref mOffset );
				ImGui.InputInt( "Map Scale Factor", ref mSizeFactor );*/

				if( mClientState.LocalPlayer != null )
				{
					Vector2 pos2D = new( mClientState.LocalPlayer.Position.X, mClientState.LocalPlayer.Position.Z );
					ImGui.Text( $"Current World Coords: X: {pos2D.X:F6}, Y: {pos2D.Y:F6}, Z: {mClientState.LocalPlayer.Position.Y:F6}" );
					var pixelCoords = GetPixelCoordinates( pos2D, mOffset, (UInt16)mSizeFactor );
					ImGui.Text( $"Map Pixel Coords: X: {pixelCoords.X:F0}, Y: {pixelCoords.Y:F0}" );
					//var normCoords = GetNormCoords( pos2D );
					//ImGui.Text( $"Map Norm Coords: X: {normCoords.X:F3}, Y: {normCoords.Y:F3}" );
					//var gameMapCoords = GetGameMapCoordinates( pos2D, mOffset, (UInt16)mSizeFactor );
					//ImGui.Text( $"Game Map Coords (1): X: {gameMapCoords.X:F6}, Y: {gameMapCoords.Y:F6}" );
					var gameMapCoords2 = WorldToMapCoordinates( pos2D, mOffset, (UInt16)mSizeFactor );
					ImGui.Text( $"Game Map Coords (Mine): X: {gameMapCoords2.X:F6}, Y: {gameMapCoords2.Y:F6}" );
					//var otherFormulaX = OtherPeoplesFormulas.ConvertCoordinatesIntoMapPosition( mSizeFactor, mOffset.X, pos2D.X );
					//var otherFormulaY = OtherPeoplesFormulas.ConvertCoordinatesIntoMapPosition( mSizeFactor, mOffset.Y, pos2D.Y );
					//var otherFormulaX = OtherPeoplesFormulas.ToMapCoordinate( pos2D.X, mSizeFactor, 41 );
					//var otherFormulaY = OtherPeoplesFormulas.ToMapCoordinate( pos2D.Y, mSizeFactor, 41 );
					//var otherFormulaXCorrected = OtherPeoplesFormulas.ToMapCoordinate( pos2D.X, mSizeFactor, 40.96 );
					//var otherFormulaYCorrected = OtherPeoplesFormulas.ToMapCoordinate( pos2D.Y, mSizeFactor, 40.96 );
					//ImGui.Text( $"Game Map Coords (Magic Number 41): X: {otherFormulaX:F3}, Y: {otherFormulaY:F3}" );
					//ImGui.Text( $"Game Map Coords (Magic Number 40.96): X: {otherFormulaXCorrected:F3}, Y: {otherFormulaYCorrected:F3}" );
					var otherOffsetFormulaX = OtherPeoplesFormulas.ConvertCoordinatesIntoMapPosition( mSizeFactor, mOffset.X, pos2D.X, 41 );
					var otherOffsetFormulaY = OtherPeoplesFormulas.ConvertCoordinatesIntoMapPosition( mSizeFactor, mOffset.Y, pos2D.Y, 41 );
					var otherOffsetFormulaXCorrected = OtherPeoplesFormulas.ConvertCoordinatesIntoMapPosition( mSizeFactor, mOffset.X, pos2D.X, 40.96 );
					var otherOffsetFormulaYCorrected = OtherPeoplesFormulas.ConvertCoordinatesIntoMapPosition( mSizeFactor, mOffset.Y, pos2D.Y, 40.96 );
					ImGui.Text( $"Game Map Coords (Mappy (41)): X: {otherOffsetFormulaX:F6}, Y: {otherOffsetFormulaY:F6}" );
					ImGui.Text( $"Game Map Coords (Mappy Modified (40.96)): X: {otherOffsetFormulaXCorrected:F6}, Y: {otherOffsetFormulaYCorrected:F6}" );
					var KazFormula_X = OtherPeoplesFormulas.ConvertMapXZPositionToCoordinate_Kaz( mSizeFactor, mOffset.X, pos2D.X );
					var KazFormula_Y = OtherPeoplesFormulas.ConvertMapXZPositionToCoordinate_Kaz( mSizeFactor, mOffset.Y, pos2D.Y );
					ImGui.Text( $"Game Map Coords (Kaz's): X: {KazFormula_X:F6}, Y: {KazFormula_Y:F6}" );
				}
			}
			ImGui.End();
		}

		unsafe protected string GetMapID()
		{
			var pMiniMap = (AtkUnitBase*) mGameGui.GetAddonByName( "_NaviMap", 1 );
			var mapTexturePath = "";

			if( pMiniMap != null )
			{
				for( var i = 0; i != pMiniMap->UldManager.NodeListCount; ++i )
				{
					if( pMiniMap->UldManager.NodeList[i]->NodeID == 18 )
					{
						var pSubNode = (AtkComponentNode*) pMiniMap->UldManager.NodeList[i];

						for( var j = 0; j != pSubNode->Component->UldManager.NodeListCount; ++j )
						{
							if( pSubNode != null && pSubNode->Component->UldManager.NodeList[j]->NodeID == 6 )
							{
								var imageNode = (AtkImageNode*) pSubNode->Component->UldManager.NodeList[j];
								var texFileNamePtr =
									imageNode->PartsList->Parts[imageNode->PartId].UldAsset->AtkTexture.Resource->TexFileResourceHandle
										->ResourceHandle.FileName;
								mapTexturePath = Marshal.PtrToStringAnsi( new IntPtr( texFileNamePtr.BufferPtr ) );
							}
						}
					}
				}
			}
			string[] pathParts = mapTexturePath.Split( '/', StringSplitOptions.RemoveEmptyEntries );
			string mapID = "";
			if( pathParts.Length > 4 )
			{
				mapID = pathParts[2] + "/" + pathParts[3];
			}

			return mapID;
		}

		protected Vector2 GetWorldCoordinates( Vector2 pixelCoordinates )
		{
			return ( pixelCoordinates - new Vector2( 1024f ) ) / (float)mSizeFactor * 100f - mOffset;
		}
		protected Vector2 GetPixelCoordinates( Vector2 worldXZCoordinates, Vector2 mapOffset, UInt16 mapSizeFactor )
		{
			return ( worldXZCoordinates + mapOffset ) / 100f * mapSizeFactor + new Vector2( 1024f );
		}
		protected Vector2 GetNormCoords( Vector2 mapCoordinates )
		{
			return ( mapCoordinates + mOffset ) / (float)mSizeFactor;
		}
		/*protected Vector2 GetGameMapCoordinates( Vector2 worldXZCoordinates, Vector2 mapOffset, UInt16 mapSizeFactor )
		{
			return Truncate( ( worldXZCoordinates + mapOffset ) / (float)mapSizeFactor * 2f + Vector2.One, 1 );
		}*/

		/*protected Vector2 GetGameMapCoordinates2( Vector2 worldXZCoordinates, Vector2 mapOffset, UInt16 mapSizeFactor )
		{
			const float magicScaleFactor = 1E5f;
			Int32 X = (Int32)( worldXZCoordinates.X * magicScaleFactor + mapOffset.X );
			Int32 Y = (Int32)( worldXZCoordinates.Y * magicScaleFactor + mapOffset.Y );
			X /= mapSizeFactor;
			Y /= mapSizeFactor;
			return new Vector2( X, Y ) * 2f / magicScaleFactor + Vector2.One;
		}*/

		protected Vector2 GetGameMapCoordinates( Vector2 mapPixelCoordinates, UInt16 mapSizeFactor )
		{
			return mapPixelCoordinates / mapSizeFactor * 2f + Vector2.One;
		}

		protected Vector2 WorldToMapCoordinates( Vector2 worldXZCoordinates, Vector2 mapOffset, UInt16 mapSizeFactor )
		{
			return GetGameMapCoordinates( GetPixelCoordinates( worldXZCoordinates, mapOffset, mapSizeFactor ), mapSizeFactor );
		}

		protected static Vector2 Truncate( Vector2 val, int places = 0 )
		{
			double scalar = Math.Pow( 10, places );
			var x = (float) Math.Floor( val.X * scalar ) / scalar;
			var y = (float) Math.Floor( val.Y * scalar ) / scalar;
			return new Vector2( (float)x, (float)y );
		}

		protected ClientState mClientState;
		protected GameGui mGameGui;
		protected Vector2 mOffset = Vector2.Zero;
		protected int mSizeFactor = 100;

		public bool ShowWindow = true;

		protected Dictionary<string, Tuple<UInt16, Int16, Int16>> MapDict = new();

		protected class OtherPeoplesFormulas
		{
			public static double ConvertCoordinatesIntoMapPosition( double scale, double offset, double val, double magicNumber )
			{
				scale /= 100.0;
				val = Math.Round( val, 3 );
				val = ( val + offset ) * scale;
				return ( ( magicNumber / scale ) * ( ( val + 1024.0 ) / 2048.0 ) ) + 1;
			}

			public static double ToMapCoordinate( double val, double sizeFactor, double magicNumber )
			{
				var c = sizeFactor / 100.0;
				val *= c;
				return ( ( magicNumber / c ) * ( ( val + 1024.0 ) / 2048.0 ) ) + 1;
			}

			/*public static double ToMapCoordinate( double val, double sizeFactor )
			{
				var c = TerritoryType.Map.SizeFactor / 100.0;

				return ( 41.0 / c ) * ( ( val ) / 2048.0 ) + 1;
			}

			public static int ConvertMapPositionToPixels( double value, double scale )
			{
				return Convert.ToInt32( ( value - 1 ) * 50 * scale );
			}*/

			//	https://gitlab.com/jules/cl_showpos/-/blob/main/cl_showpos/Utils/MapUtil.cs
			public static float ConvertMapXZPositionToCoordinate_Kaz( float scale, double offset, float value )
			{
				var sizeFactor = scale / 100f;

				return (float)( 10 - ( ( value - -offset ) * sizeFactor + 1024f ) * -0.2f / sizeFactor ) / 10;
			}
		}
	}
}
