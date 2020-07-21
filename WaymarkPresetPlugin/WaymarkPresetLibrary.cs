using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;

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

		public int ImportPreset( byte[] rawData )
		{
			try
			{
				WaymarkPreset importedPreset = WaymarkPreset.Parse( rawData );
				importedPreset.Name = "Imported";
				Presets.Add( importedPreset );
				return Presets.Count - 1;
			}
			catch
			{
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
					return -1;
				}
			}
			catch
			{
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
