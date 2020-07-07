using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaymarkPresetPlugin
{
	public class Waymark
	{
		public Waymark()
		{
		}

		public Waymark( Waymark objToCopy )
		{
			X = objToCopy.X;
			Y = objToCopy.Y;
			Z = objToCopy.Z;
			ID = objToCopy.ID;
			Active = objToCopy.Active;
		}
		public string GetWaymarkDataString()
		{
			return Active ? ( X.ToString( " 000.00;-000.00" ) + ", " + Y.ToString( " 000.00;-000.00" ) + ", " + Z.ToString( " 000.00;-000.00" ) ) : "Unused";
		}

		public float X { get; set; } = 0.0f;
		public float Y { get; set; } = 0.0f;
		public float Z { get; set; } = 0.0f;
		public int ID { get; set; } = 0;//This is kind of a BS field, but keep it so that PP presets can be a direct import.
		public bool Active { get; set; } = false;
	}
}
