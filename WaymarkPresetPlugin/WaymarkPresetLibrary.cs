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
		public int ImportPreset( uint index )
		{
			//*****TODO: Implement importing from game slot by index*****
			return -1;
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

		//*****TODO: Implement deleting presets by index and reordering/sorting them.*****
		public List<WaymarkPreset> Presets { get; set; } = new List<WaymarkPreset>();
	}
}
