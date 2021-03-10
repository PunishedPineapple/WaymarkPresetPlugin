using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dalamud.Plugin;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace WaymarkPresetPlugin
{
	public static class ZoneInfoHandler
	{
		public static void Init( DalamudPluginInterface pluginInterface )
		{
			//	Get the game sheets that we need to populate a zone dictionary.
			ExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType> territorySheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>();
			ExcelSheet<Lumina.Excel.GeneratedSheets.ContentFinderCondition> contentFinderSheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.ContentFinderCondition>();
			ExcelSheet<Lumina.Excel.GeneratedSheets.Map> mapSheet = pluginInterface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Map>();

			//	Clean out anything that we had before.
			mZoneInfoDict.Clear();
			mTerritoryTypeIDToContentFinderIDDict.Clear();

			//	Populate the zero entries ahead of time since there may be a many to one relationship with some zero IDs.
			mZoneInfoDict[0] = ZoneInfo.Unknown;
			mTerritoryTypeIDToContentFinderIDDict[0] = 0;

			//	Get the name for every "MapID" that is an instance zone.  This is spread out over a few different sheets.  The ID number that gets used in the actual preset is the column 10 in
			//	TerritoryType.  The zone name is correlated in PlaceName, and the duty name and ContentLink IDs are in ContentFinderCondition.  We are using the Content link because that's what's
			//	returned by the best (working) function that I have been able to find so far for the current instance zone.  Confusingly, as scope has changed a bit, we want to store the actual
			//	ID of the maps for these zones too.  The best solution (for the time being) seems to be to store a pseudo map name string (the base of the map names for that zone) that can be cross-referenced later.
			foreach( TerritoryType zone in territorySheet.ToList() )
			{
				if( !mZoneInfoDict.ContainsKey( zone.Unknown10 ) && (
						zone.ExclusiveType == 2 ||
						mBodgeIncludeContentFinderConditionIDs.Contains( zone.Unknown10 )
					) )
				{
					ContentFinderCondition contentRow = contentFinderSheet.GetRow( zone.Unknown10 );
					if(	contentRow != null && (
							( contentRow.ContentLinkType > 0 && contentRow.ContentLinkType < 3 ) ||
							mBodgeIncludeContentFinderConditionIDs.Contains( zone.Unknown10 )
						) )
					{
						if( !mZoneInfoDict.ContainsKey( zone.Unknown10 ) )
						{
							string dutyName = contentRow.Name.ToString().Trim();
							if( dutyName.Length > 0 )
							{
								dutyName = dutyName.First().ToString().ToUpper() + dutyName.Substring( 1 );
							}
							mZoneInfoDict.Add( zone.Unknown10, new ZoneInfo( dutyName, zone.PlaceName.Value.Name, zone.RowId, zone.Map.Value.Id.ToString().Split( '/' )[0], zone.Unknown10, contentRow.Content ) );
						}
						if( !mTerritoryTypeIDToContentFinderIDDict.ContainsKey( zone.RowId ) )
						{
							mTerritoryTypeIDToContentFinderIDDict.Add( zone.RowId, zone.Unknown10 );
						}
					}
				}
			}

			//	Now get all of the map info for each territory.  We're doing it this way rather than solely taking the map column
			//	from the TerritoryType sheet because it's easier to handle when a territory has multiple maps this way, rather than
			//	testing each map name for something other than a "/00" and then incrementing until we find where the maps stop existing.
			foreach( Map map in mapSheet.ToList() )
			{
				string mapZoneKey = map.Id.ToString().Split( '/' )[0];

				if( !mMapInfoDict.ContainsKey( mapZoneKey ) )
				{
					mMapInfoDict.Add( mapZoneKey, new List<MapInfo>() );
				}

				mMapInfoDict[mapZoneKey].Add( new MapInfo( map.Id, map.SizeFactor, map.OffsetX, map.OffsetY, map.PlaceNameSub.Value.Name ) );
			}
		}

		public static bool IsKnownContentFinderID( UInt16 ID )
		{
			return ID != 0 && mZoneInfoDict.ContainsKey( ID );
		}

		public static ZoneInfo GetZoneInfoFromContentFinderID( UInt16 ID )
		{
			if( mZoneInfoDict.ContainsKey( ID ) )
			{
				return mZoneInfoDict[ID];
			}
			else
			{
				return mZoneInfoDict[0];
			}
		}

		public static ZoneInfo GetZoneInfoFromTerritoryTypeID( uint ID )
		{
			UInt16 contentFinderID = GetContentFinderIDFromTerritoryTypeID( ID );
			if( mZoneInfoDict.ContainsKey( contentFinderID ) )
			{
				return mZoneInfoDict[contentFinderID];
			}
			else
			{
				return mZoneInfoDict[0];
			}
		}

		public static UInt16 GetContentFinderIDFromTerritoryTypeID( uint ID )
		{
			if( mTerritoryTypeIDToContentFinderIDDict.ContainsKey( ID ) )
			{
				return mTerritoryTypeIDToContentFinderIDDict[ID];
			}
			else
			{
				return mTerritoryTypeIDToContentFinderIDDict[0];
			}
		}

		//*****TODO: Make this class enumerable instead of doing this, but that sounds like work...*****
		public static Dictionary<UInt16, ZoneInfo> GetAllZoneInfo()
		{
			return mZoneInfoDict;
		}

		public static MapInfo[] GetMapInfoFromTerritoryTypeID( uint ID )
		{
			string mapBaseName = GetZoneInfoFromTerritoryTypeID( ID ).MapBaseName;
			if( mMapInfoDict.ContainsKey( mapBaseName ) )
			{
				return mMapInfoDict[mapBaseName].ToArray();
			}
			else
			{
				return new MapInfo[0];
			}
		}

		private static Dictionary<UInt16, ZoneInfo> mZoneInfoDict = new Dictionary<ushort, ZoneInfo>();
		private static Dictionary<uint, UInt16> mTerritoryTypeIDToContentFinderIDDict = new Dictionary<uint, ushort>();
		private static Dictionary<string, List<MapInfo>> mMapInfoDict = new Dictionary<string, List<MapInfo>>();

		//	This is to hard-code that some zones should be included, even if they don't otherwise meet the criteria.  There are a small handful of
		//	ContentFinderCondition IDs that support waymark presets, but are content link type #3, and don't otherwise distinguish themselves in the
		//	sheets in any way that I have found.  There is a separate function that gets called at runtime that determines whether presets are allowed
		//	based on some flag about the current duty, but it doesn't seem to have something in the sheets that corresponds to it perfectly.
		private static List<UInt16> mBodgeIncludeContentFinderConditionIDs = new List<UInt16>{
			760,	// Delubrum Reginae
			761		// Delubrum Reginae (Savage)
		};
	}

	public struct ZoneInfo
	{
		public ZoneInfo( string dutyName, string zoneName, uint territoryTypeID, string mapBaseName, UInt16 contentFinderConditionID, uint contentLinkID )
		{
			DutyName = dutyName;
			ZoneName = zoneName;
			TerritoryTypeID = territoryTypeID;
			MapBaseName = mapBaseName;
			ContentFinderConditionID = contentFinderConditionID;
			ContentLinkID = contentLinkID;
		}

		public static readonly ZoneInfo Unknown = new ZoneInfo( "Unknown Duty", "Unknown Zone", 0, "default", 0, 0 );

		public string ZoneName { get; set; }
		public string DutyName { get; set; }
		public uint TerritoryTypeID { get; set; }
		public string MapBaseName { get; set; }
		public UInt16 ContentFinderConditionID { get; set; }
		public uint ContentLinkID { get; set; }
	}

	public struct MapInfo
	{
		public MapInfo( string mapID, UInt16 sizeFactor, Int16 offsetX, Int16 offsetY, string placeNameSub )
		{
			MapID = mapID;
			SizeFactor = sizeFactor;
			Offset = new Vector2( offsetX, offsetY );
			PlaceNameSub = placeNameSub;
		}

		public static readonly MapInfo Unknown = new MapInfo( "default/00", 100, 0, 0, "" );

		public string MapID { get; set; }
		public UInt16 SizeFactor { get; set; }
		public Vector2 Offset { get; set; }
		public string PlaceNameSub { get; set; }
		public string GetMapFilePath( bool smallMap = false )
		{
			return $"ui/map/{MapID}/{MapID.Replace( "/", "" )}_{(smallMap ? "s" : "m")}.tex";
		}
		public string GetMapParchmentImageFilePath( bool smallMap = false )
		{
			return $"ui/map/{MapID}/{MapID.Replace( "/", "" )}m_{( smallMap ? "s" : "m" )}.tex";
		}
		public Vector2 GetMapCoordinates( Vector2 pixelCoordinates )
		{
			return ( pixelCoordinates - new Vector2( 1024.0f, 1024.0f ) ) / (float)SizeFactor * 100.0f - Offset;
		}
		public Vector2 GetPixelCoordinates( Vector2 mapCoordinates )
		{
			return ( mapCoordinates + Offset ) / 100.0f * (float)SizeFactor + new Vector2( 1024.0f, 1024.0f );
		}
	}
}
