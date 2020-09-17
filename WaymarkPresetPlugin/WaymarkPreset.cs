using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WaymarkPresetPlugin
{
	[JsonObject( ItemNullValueHandling = NullValueHandling.Ignore )]	//	Shouldn't have any null waymarks, but just in case...
	public class WaymarkPreset : IEquatable<WaymarkPreset>
	{
		public WaymarkPreset()
		{
		}

		public WaymarkPreset( WaymarkPreset objToCopy )
		{
			if( objToCopy != null )
			{
				Name = objToCopy.Name;
				MapID = objToCopy.MapID;
				Time = objToCopy.Time;
				A = new Waymark( objToCopy.A );
				B = new Waymark( objToCopy.B );
				C = new Waymark( objToCopy.C );
				D = new Waymark( objToCopy.D );
				One = new Waymark( objToCopy.One );
				Two = new Waymark( objToCopy.Two );
				Three = new Waymark( objToCopy.Three );
				Four = new Waymark( objToCopy.Four );
			}
		}

		public static WaymarkPreset Parse( byte[] rawData )
		{
			if( rawData.Length != 104 )
			{
				throw new Exception( "Unexpected data length in WaymarkPreset.Parse" );
			}
			WaymarkPreset preset = new WaymarkPreset();

			preset.A.X = BitConverter.ToInt32( rawData, 0 );
			preset.A.Y = BitConverter.ToInt32( rawData, 4 );
			preset.A.Z = BitConverter.ToInt32( rawData, 8 );
			preset.A.Active = ( rawData[96] & 0b00000001 ) > 0;
			preset.A.ID = 0;

			preset.B.X = BitConverter.ToInt32( rawData, 12 );
			preset.B.Y = BitConverter.ToInt32( rawData, 16 );
			preset.B.Z = BitConverter.ToInt32( rawData, 20 );
			preset.B.Active = ( rawData[96] & 0b00000010 ) > 0;
			preset.B.ID = 1;

			preset.C.X = BitConverter.ToInt32( rawData, 24 );
			preset.C.Y = BitConverter.ToInt32( rawData, 28 );
			preset.C.Z = BitConverter.ToInt32( rawData, 32 );
			preset.C.Active = ( rawData[96] & 0b00000100 ) > 0;
			preset.C.ID = 2;

			preset.D.X = BitConverter.ToInt32( rawData, 36 );
			preset.D.Y = BitConverter.ToInt32( rawData, 40 );
			preset.D.Z = BitConverter.ToInt32( rawData, 44 );
			preset.D.Active = ( rawData[96] & 0b00001000 ) > 0;
			preset.D.ID = 3;

			preset.One.X = BitConverter.ToInt32( rawData, 48 );
			preset.One.Y = BitConverter.ToInt32( rawData, 52 );
			preset.One.Z = BitConverter.ToInt32( rawData, 56 );
			preset.One.Active = ( rawData[96] & 0b00010000 ) > 0;
			preset.One.ID = 4;

			preset.Two.X = BitConverter.ToInt32( rawData, 60 );
			preset.Two.Y = BitConverter.ToInt32( rawData, 64 );
			preset.Two.Z = BitConverter.ToInt32( rawData, 68 );
			preset.Two.Active = ( rawData[96] & 0b00100000 ) > 0;
			preset.Two.ID = 5;

			preset.Three.X = BitConverter.ToInt32( rawData, 72 );
			preset.Three.Y = BitConverter.ToInt32( rawData, 76 );
			preset.Three.Z = BitConverter.ToInt32( rawData, 80 );
			preset.Three.Active = ( rawData[96] & 0b01000000 ) > 0;
			preset.Three.ID = 6;

			preset.Four.X = BitConverter.ToInt32( rawData, 84 );
			preset.Four.Y = BitConverter.ToInt32( rawData, 88 );
			preset.Four.Z = BitConverter.ToInt32( rawData, 92 );
			preset.Four.Active = ( rawData[96] & 0b10000000 ) > 0;
			preset.Four.ID = 7;

			preset.MapID = BitConverter.ToUInt16( rawData, 98 );

			preset.Time = DateTimeOffset.FromUnixTimeSeconds( BitConverter.ToInt32( rawData, 100 ) );

			return preset;
		}

		public byte[] ConstructGamePreset()
		{
			//	List is easy because we can just push data on to it.
			List<byte> byteData = new List<byte>();

			//	Waymark coordinates.
			byteData.AddRange( BitConverter.GetBytes( A.Active ? A.X : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( A.Active ? A.Y : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( A.Active ? A.Z : 0 ) );

			byteData.AddRange( BitConverter.GetBytes( B.Active ? B.X : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( B.Active ? B.Y : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( B.Active ? B.Z : 0 ) );

			byteData.AddRange( BitConverter.GetBytes( C.Active ? C.X : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( C.Active ? C.Y : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( C.Active ? C.Z : 0 ) );

			byteData.AddRange( BitConverter.GetBytes( D.Active ? D.X : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( D.Active ? D.Y : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( D.Active ? D.Z : 0 ) );

			byteData.AddRange( BitConverter.GetBytes( One.Active ? One.X : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( One.Active ? One.Y : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( One.Active ? One.Z : 0 ) );

			byteData.AddRange( BitConverter.GetBytes( Two.Active ? Two.X : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( Two.Active ? Two.Y : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( Two.Active ? Two.Z : 0 ) );

			byteData.AddRange( BitConverter.GetBytes( Three.Active ? Three.X : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( Three.Active ? Three.Y : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( Three.Active ? Three.Z : 0 ) );

			byteData.AddRange( BitConverter.GetBytes( Four.Active ? Four.X : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( Four.Active ? Four.Y : 0 ) );
			byteData.AddRange( BitConverter.GetBytes( Four.Active ? Four.Z : 0 ) );

			//	Which waymarks are active.
			byte activeMask = 0x00;
			if( A.Active )		activeMask |= 0b00000001;
			if( B.Active )		activeMask |= 0b00000010;
			if( C.Active )		activeMask |= 0b00000100;
			if( D.Active )		activeMask |= 0b00001000;
			if( One.Active )	activeMask |= 0b00010000;
			if( Two.Active )	activeMask |= 0b00100000;
			if( Three.Active )	activeMask |= 0b01000000;
			if( Four.Active )	activeMask |= 0b10000000;
			byteData.Add( activeMask );

			//	Reserved byte.
			byteData.Add( (byte)0x00 );

			//	Territory ID.
			byteData.AddRange( BitConverter.GetBytes( MapID ) );

			//	Time last modified.
			byteData.AddRange( BitConverter.GetBytes( (Int32)Time.ToUnixTimeSeconds() ) );

			//	Shouldn't ever come up with the wrong length, but just in case...
			if( byteData.Count != 104 )
			{
				throw new Exception( $"Error in WaymarkPreset.ConstructGamePreset(): Constructed byte array was of an unexpected length." );
			}

			//	Send it out.
			return byteData.ToArray();
		}

		public string GetPresetDataString( GetZoneNameDelegate dGetZoneName = null, bool showIDToo = false )
		{
			//	Try to get the zone name from the function passed to us if we can.
			string zoneName = "";
			if( dGetZoneName != null )
			{
				try
				{
					zoneName = dGetZoneName( MapID, showIDToo );
				}
				catch
				{
					zoneName = "Error retrieving zone name!";
				}
			}

			//	Construct the string.
			string str = "";
			str += "A: " + A.GetWaymarkDataString() + "\r\n";
			str += "B: " + B.GetWaymarkDataString() + "\r\n";
			str += "C: " + C.GetWaymarkDataString() + "\r\n";
			str += "D: " + D.GetWaymarkDataString() + "\r\n";
			str += "1: " + One.GetWaymarkDataString() + "\r\n";
			str += "2: " + Two.GetWaymarkDataString() + "\r\n";
			str += "3: " + Three.GetWaymarkDataString() + "\r\n";
			str += "4: " + Four.GetWaymarkDataString() + "\r\n";
			str += "Zone: " + zoneName + "\r\n";
			str += "Last Modified: " + Time.LocalDateTime.ToString();
			return str;
		}

		public delegate string GetZoneNameDelegate( UInt16 zoneID, bool showID );

		//	This looks gross, but it's easier to be compatible with PP presets if we have each waymark be a named member instead of in a collection :(
		public string Name = "Unknown";
		[JsonConverter( typeof( MapIDJsonConverter ) )] public UInt16 MapID { get; set; } = 0;	//PP sometimes gives bogus MapIDs that are outside the UInt16, so use a converter to handle those.
		public DateTimeOffset Time { get; set; } = new DateTimeOffset( DateTimeOffset.Now.UtcDateTime );
		public Waymark A { get; set; } = new Waymark("A");
		public Waymark B { get; set; } = new Waymark("B");
		public Waymark C { get; set; } = new Waymark("C");
		public Waymark D { get; set; } = new Waymark("D");
		public Waymark One { get; set; } = new Waymark("1");
		public Waymark Two { get; set; } = new Waymark("2");
		public Waymark Three { get; set; } = new Waymark("3");
		public Waymark Four { get; set; } = new Waymark("4");

		[JsonIgnore]
		public IEnumerable<Waymark> Waymarks
		{
			get
			{
				yield return A;
				yield return B;
				yield return C;
				yield return D;
				yield return One;
				yield return Two;
				yield return Three;
				yield return Four;
			}
		}

		public virtual bool ShouldSerializeTime()	//More JSON bullshit because it has to be polymorphic for the serializer to check it in a derived class apparently.
		{
			return true;
		}

		public bool Equals(WaymarkPreset other)
		{
			return A.Equals(other.A)
				&& B.Equals(other.B)
				&& C.Equals(other.C)
				&& D.Equals(other.D)
				&& One.Equals(other.One)
				&& Two.Equals(other.Two)
				&& Three.Equals(other.Three)
				&& Four.Equals(other.Four)
				&& MapID == other.MapID;
		}
	}

	//	We may be getting the MapID as something that won't fit in UInt16, so this class helps us handle that.
	public class MapIDJsonConverter : JsonConverter<UInt16>
	{
		public override void WriteJson( JsonWriter writer, UInt16 value, JsonSerializer serializer )
		{
			writer.WriteValue( value );
		}
		public override UInt16 ReadJson( JsonReader reader, Type objectType, UInt16 existingValue, bool hasExistingValue, JsonSerializer serializer )
		{
			long val = (long)reader.Value;
			if( val > UInt16.MaxValue || val < 0 )
			{
				return 0;
			}
			else
			{
				return (UInt16)val;
			}
		}
	}
}
