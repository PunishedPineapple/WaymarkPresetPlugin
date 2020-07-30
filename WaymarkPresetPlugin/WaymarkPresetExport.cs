using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace WaymarkPresetPlugin
{
	//	More JSON bullshit because we want the timestamp to be saved in our library, but not included in any exports.
	class WaymarkPresetExport : WaymarkPreset
	{
		public static string GetExportString( WaymarkPreset preset )
		{
			return JsonConvert.SerializeObject( new WaymarkPresetExport( preset ) );
		}
		public WaymarkPresetExport( WaymarkPreset objToCopy ) : base( objToCopy )
		{
		}
		public override bool ShouldSerializeTime()
		{
			return false;
		}
	}
}
