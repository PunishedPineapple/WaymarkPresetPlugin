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
		public bool ImportPreset( uint index )
		{
			//	We're a plugin, so we don't want to be throwing exceptions in someone else's program.  Just return that we couldn't do it if something goes wrong.
			try
			{
				//WaymarkPreset importedPreset = JsonConvert.DeserializeObject<WaymarkPreset>( importStr );
				//Presets.Add( importedPreset );
				//StreamWriter log = File.AppendText( System.Reflection.Assembly.GetExecutingAssembly().Location + ".log" );
				//log.WriteLine( "Added preset as follows: \r\n" + Presets.Last().GetPresetDataString() );
				//log.Close();
				return true;
			}
			catch( Exception e )
			{
				//*****TODO: Write errors to the Dalamud log?*****
				//StreamWriter log = File.AppendText( System.Reflection.Assembly.GetExecutingAssembly().Location + ".log" );
				//log.WriteLine( e.Message );
				//log.Close();
				return false;
			}
		}

		public bool ImportPreset( string importStr )
		{
			try
			{
				WaymarkPreset importedPreset = JsonConvert.DeserializeObject<WaymarkPreset>( importStr );
				Presets.Add( importedPreset );
				StreamWriter log = File.AppendText( "Z:\\WaymarkPresetPlugin.log" );
				log.WriteLine( "Added preset as follows: \r\n" + Presets.Last().GetPresetDataString() );
				log.Close();
				return true;
			}
			catch( Exception e )
			{
				//*****TODO: Write errors to the Dalamud log?*****
				StreamWriter log = File.AppendText( "Z:\\WaymarkPresetPlugin.log" );
				log.WriteLine( e.Message );
				log.Close();
				return false;
			}
		}

		public string ExportPreset( int index )
		{
			if( index < Presets.Count )
			{
				return JsonConvert.SerializeObject( Presets[index] );
			}
			else
			{
				return "Invalid index requested for preset export: " + index.ToString();
			}
		}
		public List<WaymarkPreset> Presets { get; set; } = new List<WaymarkPreset>();
	}
}
