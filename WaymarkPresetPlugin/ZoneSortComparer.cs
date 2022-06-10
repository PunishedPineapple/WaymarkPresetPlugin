using System;
using System.Collections.Generic;

namespace WaymarkPresetPlugin
{
	internal class ZoneSortComparer_Basic : IComparer<UInt16>
	{
		public int Compare( UInt16 a, UInt16 b )
		{
			int compareResult = a.CompareTo( b );
			return SortDescending ? -compareResult : compareResult;
		}

		internal bool SortDescending { get; set; } = false;
	}

	internal class ZoneSortComparer_Alphabetical : IComparer<UInt16>
	{
		public int Compare( UInt16 a, UInt16 b )
		{
			int compareResult;

			bool aValid = ZoneInfoHandler.IsKnownContentFinderID( a );
			bool bValid = ZoneInfoHandler.IsKnownContentFinderID( b );

			if( !aValid && !bValid )
			{
				compareResult = 0;
			}
			else if( !aValid )
			{
				compareResult = -1;
			}
			else if( !bValid )
			{
				compareResult = 1;
			}
			else
			{
				compareResult = String.Compare( ZoneInfoHandler.GetZoneInfoFromContentFinderID( a ).DutyName, ZoneInfoHandler.GetZoneInfoFromContentFinderID( b ).DutyName, true );
			}

			return SortDescending ? -compareResult : compareResult;
		}

		internal bool SortDescending { get; set; } = false;
	}

	internal class ZoneSortComparer_CustomOrder : IComparer<UInt16>
	{
		public int Compare( UInt16 a, UInt16 b )
		{
			int compareResult;

			//	Try to see if these numbers exist in our sort order.
			int aPos = ZoneSortOrder.FindIndex( ( UInt16 x )=>{ return x == a; } );
			int bPos = ZoneSortOrder.FindIndex( ( UInt16 x )=>{ return x == b; } );

			//	If neither exists in our sort order, compare them as numbers.
			if( aPos == -1 && bPos == -1)
			{
				compareResult = a.CompareTo( b );
			}
			//	If the value doesn't exist in our sort order, but the comparee does, it always goes at the end.
			else if( aPos == -1 && bPos != -1 )
			{
				compareResult = 1;
			}
			//	If the comparison value doesn't exist in our sort order, but the value does, the value always comes first.
			else if( aPos != -1 && bPos == -1 )
			{
				compareResult = -1;
			}
			//	Otherwise, compare the positions in the list.
			else if( aPos < bPos )
			{
				compareResult = -1;
			}
			else if( aPos > bPos )
			{
				compareResult = 1;
			}
			else
			{
				compareResult = 0;
			}

			if( SortDescending ) compareResult = -compareResult;

			return compareResult;
		}

		internal readonly List<UInt16> ZoneSortOrder = new();
		internal bool SortDescending { get; set; } = false;
	}
}
