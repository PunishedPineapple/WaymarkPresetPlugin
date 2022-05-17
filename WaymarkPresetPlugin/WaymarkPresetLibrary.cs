using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Dalamud.Logging;
using CheapLoc;

namespace WaymarkPresetPlugin
{
	public class WaymarkPresetLibrary
	{
		//***** TODO: Subscribe/unsubscribe to preset's zone change event on add/remove from library, and update sort stuff based on that.  We really need to make the presets list externally immutable though, because we're just begging for a big issue at this point.
		public int ImportPreset( WaymarkPreset preset )
		{
			WaymarkPreset importedPreset = new( preset );
			return ImportPreset_Common( importedPreset );
		}

		public int ImportPreset( GamePreset gamePresetData )
		{
			try
			{
				WaymarkPreset importedPreset = WaymarkPreset.Parse( gamePresetData );
				importedPreset.Name = Loc.Localize( "Default Preset Name (Imported)", "Imported" );
				return ImportPreset_Common( importedPreset );
			}
			catch( Exception e )
			{
				PluginLog.LogWarning( $"Error in WaymarkPresetLibrary.ImportPreset( GamePreset ):\r\n{e}" );
				return -1;
			}
		}

		public int ImportPreset( string importStr )
		{
			try
			{
				WaymarkPreset importedPreset = JsonConvert.DeserializeObject<WaymarkPreset>( importStr );
				if( importedPreset != null )
				{
					return ImportPreset_Common( importedPreset );
				}
				else
				{
					PluginLog.LogWarning( $"Error in WaymarkPresetLibrary.ImportPreset( string ): Deserialized input resulted in a null!" );
					return -1;
				}
			}
			catch( Exception e )
			{
				PluginLog.LogWarning( $"Error in WaymarkPresetLibrary.ImportPreset( string ):\r\n{e}" );
				return -1;
			}
		}

		private int ImportPreset_Common( WaymarkPreset preset )
		{
			Presets.Add( preset );
			if( mZoneSortComparer_Custom.ZoneSortOrder.Any() && !mZoneSortComparer_Custom.ZoneSortOrder.Contains( preset.MapID ) ) AddOrChangeSortEntry( preset.MapID );
			return Presets.Count - 1;
		}

		public string ExportPreset( int index )
		{
			if( index >= 0 && index < Presets.Count )
			{
				return JsonConvert.SerializeObject( Presets[index] );
			}
			else
			{
				return String.Format( Loc.Localize( "Export Preset Fallback Error", "Invalid index requested for preset export.  No preset exists at index {0}." ), index );
			}
		}

		public bool DeletePreset( int index )
		{
			if( index >= 0 && index < Presets.Count )
			{
				UInt16 presetZone = Presets[index].MapID;
				Presets.RemoveAt( index );
				if( !Presets.Where( ( WaymarkPreset x ) => { return x.MapID == presetZone; } ).Any() ) RemoveSortEntry( presetZone );
				return true;
			}
			else
			{
				return false;
			}
		}

		public int MovePreset( int indexToMove, int newPosition, bool placeAfter )
		{
			if( newPosition == indexToMove )
			{
				return indexToMove;
			}
			else if(	indexToMove >= 0 &&
						indexToMove < Presets.Count &&
						newPosition >= 0 &&
						newPosition <= Presets.Count - ( placeAfter ? 1 : 0 ) )
			{
				var preset = Presets[indexToMove];
				Presets.RemoveAt( indexToMove );
				if( newPosition > indexToMove ) --newPosition;
				if( placeAfter ) ++newPosition;
				Presets.Insert( newPosition, preset );
				//SortPresets();
				return newPosition;
			}
			else
			{
				return -1;
			}
		}

		public void AddOrChangeSortEntry( UInt16 zone, UInt16 placeBeforeZone = UInt16.MaxValue )
		{
			if( zone == placeBeforeZone ) return;

			int zoneIndex = mZoneSortComparer_Custom.ZoneSortOrder.FindIndex( ( UInt16 x ) => { return x == zone; } );
			int moveToIndex = mZoneSortComparer_Custom.ZoneSortOrder.FindIndex( ( UInt16 x ) => { return x == placeBeforeZone; } );

			if( moveToIndex == -1 )
			{
				if( zoneIndex != -1 )
				{
					mZoneSortComparer_Custom.ZoneSortOrder.RemoveAt( zoneIndex );
				}
				mZoneSortComparer_Custom.ZoneSortOrder.Add( zone );
			}
			else
			{
				if( zoneIndex != -1 )
				{
					mZoneSortComparer_Custom.ZoneSortOrder.RemoveAt( zoneIndex );
					if( zoneIndex < moveToIndex ) --moveToIndex;
				}
				mZoneSortComparer_Custom.ZoneSortOrder.Insert( moveToIndex, zone );
			}
		}

		public void RemoveSortEntry( UInt16 zone )
		{
			int zoneIndex = mZoneSortComparer_Custom.ZoneSortOrder.FindIndex( ( UInt16 x ) => { return x == zone; } );
			if( zoneIndex != -1 ) mZoneSortComparer_Custom.ZoneSortOrder.RemoveAt( zoneIndex );
		}

		public void ClearSortOrder()
		{
			mZoneSortComparer_Custom.ZoneSortOrder.Clear();
		}

		public void SetSortOrder( List<UInt16> order, bool isDescending = false )
		{
			if( isDescending ) order.Reverse();
			mZoneSortComparer_Custom.ZoneSortOrder.Clear();
			mZoneSortComparer_Custom.ZoneSortOrder.AddRange( order );
		}

		public List<UInt16> GetSortOrder()
		{
			return new( mZoneSortComparer_Custom.ZoneSortOrder );
		}

		public void SortZonesDescending( bool b )
		{
			mZoneSortComparer_Default.SortDescending = b;
			mZoneSortComparer_Custom.SortDescending = b;
		}

		public SortedDictionary<UInt16, List<int>> GetSortedIndices( bool useCustomSort )
		{
			SortedDictionary<UInt16, List<int>> sortedIndices = useCustomSort ? new( mZoneSortComparer_Custom ) : new( mZoneSortComparer_Default );

			for( int i = 0; i < Presets.Count; ++i )
			{
				if( !sortedIndices.ContainsKey( Presets[i].MapID ) )
				{
					sortedIndices.Add( Presets[i].MapID, new List<int>() );
				}

				sortedIndices[Presets[i].MapID].Add( i );
			}

			return sortedIndices;
		}

		[JsonProperty( NullValueHandling = NullValueHandling.Ignore )]	//	It shouldn't happen, but never let the deserializer overwrite this with null.
		public List<WaymarkPreset> Presets { get; protected set; } = new List<WaymarkPreset>();

		private readonly ZoneSortComparer_Basic mZoneSortComparer_Default = new ZoneSortComparer_Basic();
		private readonly ZoneSortComparer_CustomOrder mZoneSortComparer_Custom = new ZoneSortComparer_CustomOrder();
	}
}
