using System;
using System.Collections.Generic;
using System.Linq;

using CheapLoc;

using Dalamud.Logging;

using Newtonsoft.Json;

namespace WaymarkPresetPlugin
{
	public sealed class WaymarkPresetLibrary
	{
		//***** TODO: Subscribe/unsubscribe to preset's zone change event on add/remove from library, and update sort stuff based on that.  We really need to make the presets list externally immutable though, because we're just begging for a big issue at this point.
		internal int ImportPreset( WaymarkPreset preset )
		{
			WaymarkPreset importedPreset = new( preset );
			return ImportPreset_Common( importedPreset );
		}

		internal int ImportPreset( GamePreset gamePresetData )
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

		internal int ImportPreset( string importStr )
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
			if( mZoneSortComparer_Custom.ZoneSortOrder.Any() && !mZoneSortComparer_Custom.ZoneSortOrder.Contains( preset.MapID ) ) AddOrChangeCustomSortEntry( preset.MapID );
			return Presets.Count - 1;
		}

		internal string ExportPreset( int index )
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

		internal bool DeletePreset( int index )
		{
			if( index >= 0 && index < Presets.Count )
			{
				UInt16 presetZone = Presets[index].MapID;
				Presets.RemoveAt( index );
				if( !Presets.Where( ( WaymarkPreset x ) => { return x.MapID == presetZone; } ).Any() ) RemoveCustomSortEntry( presetZone );
				return true;
			}
			else
			{
				return false;
			}
		}

		internal int MovePreset( int indexToMove, int newPosition, bool placeAfter )
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

		internal void AddOrChangeCustomSortEntry( UInt16 zone, UInt16 placeBeforeZone = UInt16.MaxValue )
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

		internal void RemoveCustomSortEntry( UInt16 zone )
		{
			int zoneIndex = mZoneSortComparer_Custom.ZoneSortOrder.FindIndex( ( UInt16 x ) => { return x == zone; } );
			if( zoneIndex != -1 ) mZoneSortComparer_Custom.ZoneSortOrder.RemoveAt( zoneIndex );
		}

		internal void ClearCustomSortOrder()
		{
			mZoneSortComparer_Custom.ZoneSortOrder.Clear();
		}

		internal void SetCustomSortOrder( List<UInt16> order, bool isDescending = false )
		{
			if( isDescending ) order.Reverse();
			mZoneSortComparer_Custom.ZoneSortOrder.Clear();
			mZoneSortComparer_Custom.ZoneSortOrder.AddRange( order );
		}

		internal List<UInt16> GetCustomSortOrder()
		{
			return new( mZoneSortComparer_Custom.ZoneSortOrder );
		}

		internal void SetZoneSortDescending( bool b )
		{
			mZoneSortComparer_Default.SortDescending = b;
			mZoneSortComparer_Alphabetical.SortDescending = b;
			mZoneSortComparer_Custom.SortDescending = b;
		}

		internal SortedDictionary<UInt16, List<int>> GetSortedIndices( ZoneSortType sortType )
		{
			IComparer<UInt16> comparer = sortType switch
			{
				ZoneSortType.Basic => mZoneSortComparer_Default,
				ZoneSortType.Alphabetical => mZoneSortComparer_Alphabetical,
				ZoneSortType.Custom => mZoneSortComparer_Custom,
				_ => mZoneSortComparer_Default,
			};

			SortedDictionary<UInt16, List<int>> sortedIndices = new( comparer );

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

		internal SortedDictionary<UInt16, List<int>> GetSortedIndices( ZoneSortType sortType, bool sortDescending )
		{
			SetZoneSortDescending( sortDescending );
			return GetSortedIndices( sortType );
		}

		[JsonProperty( NullValueHandling = NullValueHandling.Ignore )]	//	It shouldn't happen, but never let the deserializer overwrite this with null.
		internal List<WaymarkPreset> Presets { get; private set; } = new List<WaymarkPreset>();

		private readonly ZoneSortComparer_Basic mZoneSortComparer_Default = new();
		private readonly ZoneSortComparer_Alphabetical mZoneSortComparer_Alphabetical = new();
		private readonly ZoneSortComparer_CustomOrder mZoneSortComparer_Custom = new();
	}
}
