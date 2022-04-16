using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Dalamud.Logging;

namespace WaymarkPresetPlugin
{
	public class WaymarkPresetLibrary
	{
		public int ImportPreset( WaymarkPreset preset )
		{
			WaymarkPreset importedPreset = new WaymarkPreset( preset );
			Presets.Add( importedPreset );
			return Presets.Count - 1;
		}

		public int ImportPreset( GamePreset gamePresetData )
		{
			try
			{
				WaymarkPreset importedPreset = WaymarkPreset.Parse( gamePresetData );
				importedPreset.Name = "Imported";
				Presets.Add( importedPreset );
				return Presets.Count - 1;
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
					Presets.Add( importedPreset );
					return Presets.Count - 1;
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

		public string ExportPreset( int index )
		{
			if( index >= 0 && index < Presets.Count )
			{
				return JsonConvert.SerializeObject( Presets[index] );
			}
			else
			{
				return "Invalid index requested for preset export.  No preset exists at index " + index.ToString() + ".";
			}
		}

		public bool DeletePreset( int index )
		{
			if( index >= 0 && index < Presets.Count )
			{
				Presets.RemoveAt( index );
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

		public SortedDictionary<UInt16, List<int>> GetSortedIndices()
		{
			SortedDictionary<UInt16, List<int>> sortedIndices = new SortedDictionary<ushort, List<int>>();

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
	}
}
