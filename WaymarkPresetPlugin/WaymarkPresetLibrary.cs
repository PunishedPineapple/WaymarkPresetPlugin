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
			WaymarkPreset importedPreset = JsonConvert.DeserializeObject<WaymarkPreset>( importStr );
			Presets.Add( importedPreset );
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
				return "Invalid index requested for preset export.  No preset exists at index " + index.ToString() + ".";
			}
		}

		public Dictionary<UInt16, List<int>> GetSortedIndices()
		{
			Dictionary<UInt16, List<int>> sortedIndices = new Dictionary<ushort, List<int>>();

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

		//*****TODO: Implement deleting presets by index and reordering/sorting them.*****
		public List<WaymarkPreset> Presets { get; protected set; } = new List<WaymarkPreset>();
	}
}
