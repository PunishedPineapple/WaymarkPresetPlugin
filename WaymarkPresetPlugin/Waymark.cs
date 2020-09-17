using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaymarkPresetPlugin
{
	public class Waymark : IEquatable<Waymark>
	{
		public Waymark()
		{
		}

		public Waymark(string Label)
		{
			this.Label = Label;
		}
		
		public Waymark( Waymark objToCopy )
		{
			if( objToCopy != null )
			{
				X = objToCopy.X;
				Y = objToCopy.Y;
				Z = objToCopy.Z;
				ID = objToCopy.ID;
				Active = objToCopy.Active;
			}
		}
		public string GetWaymarkDataString()
		{
			return Active ? ( (X / 1000f).ToString( "0.00" ).PadLeft( 7 ) + ", " + (Y / 1000f).ToString( "0.00" ).PadLeft( 7 ) + ", " + (Z / 1000f).ToString( "0.00" ).PadLeft( 7 ) ) : "Unused";
		}

		public int X { get; set; } = 0;
		public int Y { get; set; } = 0;
		public int Z { get; set; } = 0;
		public int ID { get; set; } = 0;	//This is kind of a BS field, but keep it for import/export interop with PP.
		public string Label { get; set; }
		public bool Active = false;

		public bool Equals(Waymark other)
		{
			return X == other.X
				&& Y == other.Y
				&& Z == other.Z;
		}
	}
}
