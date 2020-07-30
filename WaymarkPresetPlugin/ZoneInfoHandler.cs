using System;
using System.Collections.Generic;
using System.Linq;
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

			//	Clean out anything that we had before.
			mZoneInfoDict.Clear();
			mTerritoryTypeIDToContentFinderIDDict.Clear();

			//	Populate the zero entries ahead of time since there may be a many to one relationship with some zero IDs.
			mZoneInfoDict[0] = ZoneInfo.Unknown;
			mTerritoryTypeIDToContentFinderIDDict[0] = 0;

			//	Get the name for every "MapID" that is an instance zone.  This is spread out over a few different sheets.  The ID number that gets used in the actual preset is the column 10 in
			//	TerritoryType.  The zone name is correlated in PlaceName, and the duty name and ContentLink IDs are in ContentFinderCondition.  We are using the Content link because that's what's
			//	returned by the best (working) function that I have been able to find so far for the current instance zone.
			foreach( TerritoryType zone in territorySheet.ToList() )
			{
				if( zone.ExclusiveType == 2 && !mZoneInfoDict.ContainsKey( zone.Unknown10 ) )
				{
					ContentFinderCondition contentRow = contentFinderSheet.GetRow( zone.Unknown10 );
					if( contentRow != null &&
						contentRow.ContentLinkType > 0 &&
						contentRow.ContentLinkType < 3 )
					{
						if( !mZoneInfoDict.ContainsKey( zone.Unknown10 ) )
						{
							string dutyName = contentRow.Name.Trim();
							if( dutyName.Length > 0 )
							{
								dutyName = dutyName.First().ToString().ToUpper() + dutyName.Substring( 1 );
							}
							mZoneInfoDict.Add( zone.Unknown10, new ZoneInfo( dutyName, zone.PlaceName.Value.Name, zone.RowId, zone.Unknown10, contentRow.Content ) );
						}
						if( !mTerritoryTypeIDToContentFinderIDDict.ContainsKey( zone.RowId ) )
						{
							mTerritoryTypeIDToContentFinderIDDict.Add( zone.RowId, zone.Unknown10 );
						}
					}
				}
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

		private static Dictionary<UInt16, ZoneInfo> mZoneInfoDict = new Dictionary<ushort, ZoneInfo>();
		private static Dictionary<uint, UInt16> mTerritoryTypeIDToContentFinderIDDict = new Dictionary<uint, ushort>();
	}

	public struct ZoneInfo
	{
		public ZoneInfo( string dutyName, string zoneName, uint territoryTypeID, UInt16 contentFinderConditionID, uint contentLinkID )
		{
			DutyName = dutyName;
			ZoneName = zoneName;
			TerritoryTypeID = territoryTypeID;
			ContentFinderConditionID = contentFinderConditionID;
			ContentLinkID = contentLinkID;
		}

		public static readonly ZoneInfo Unknown = new ZoneInfo( "Unknown Duty", "Unknown Zone", 0, 0, 0 );

		public string ZoneName { get; set; }
		public string DutyName { get; set; }
		public uint TerritoryTypeID { get; set; }
		public UInt16 ContentFinderConditionID { get; set; }
		public uint ContentLinkID { get; set; }
	}
}
