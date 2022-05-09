using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace WaymarkPresetPlugin
{
	//	We need this because we can't pass the properties from the regular Waymark class as refs to ImGui stuff.
	internal sealed class ScratchPreset
	{
		public class ScratchWaymark
		{
			public float X;
			public float Y;
			public float Z;
			public int ID;
			public bool Active;
			public string Label;
		}

		public void SetWaymark( int index, bool active, Vector3 coords )
		{
			if( index >= 0 && index < Waymarks.Count )
			{
				Waymarks[index].Active = active;
				Waymarks[index].X = coords.X;
				Waymarks[index].Y = coords.Y;
				Waymarks[index].Z = coords.Z;
			}
		}

		public void SwapWaymarks( int index1, int index2 )
		{
			if( index1 == index2 ||
				index1 < 0 ||
				index2 < 0 ||
				index1 >= Waymarks.Count ||
				index2 >= Waymarks.Count )
				return;

			bool tempActive = Waymarks[index1].Active;
			float tempX = Waymarks[index1].X;
			float tempY = Waymarks[index1].Y;
			float tempZ = Waymarks[index1].Z;

			Waymarks[index1].Active = Waymarks[index2].Active;
			Waymarks[index1].X = Waymarks[index2].X;
			Waymarks[index1].Y = Waymarks[index2].Y;
			Waymarks[index1].Z = Waymarks[index2].Z;

			Waymarks[index2].Active = tempActive;
			Waymarks[index2].X = tempX;
			Waymarks[index2].Y = tempY;
			Waymarks[index2].Z = tempZ;
		}

		public ScratchPreset( WaymarkPreset preset )
		{
			Name = preset.Name;
			MapID = preset.MapID;
			Waymarks = new();

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.A.X;
			Waymarks.Last().Y = preset.A.Y;
			Waymarks.Last().Z = preset.A.Z;
			Waymarks.Last().ID = preset.A.ID;
			Waymarks.Last().Active = preset.A.Active;
			Waymarks.Last().Label = "A";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.B.X;
			Waymarks.Last().Y = preset.B.Y;
			Waymarks.Last().Z = preset.B.Z;
			Waymarks.Last().ID = preset.B.ID;
			Waymarks.Last().Active = preset.B.Active;
			Waymarks.Last().Label = "B";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.C.X;
			Waymarks.Last().Y = preset.C.Y;
			Waymarks.Last().Z = preset.C.Z;
			Waymarks.Last().ID = preset.C.ID;
			Waymarks.Last().Active = preset.C.Active;
			Waymarks.Last().Label = "C";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.D.X;
			Waymarks.Last().Y = preset.D.Y;
			Waymarks.Last().Z = preset.D.Z;
			Waymarks.Last().ID = preset.D.ID;
			Waymarks.Last().Active = preset.D.Active;
			Waymarks.Last().Label = "D";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.One.X;
			Waymarks.Last().Y = preset.One.Y;
			Waymarks.Last().Z = preset.One.Z;
			Waymarks.Last().ID = preset.One.ID;
			Waymarks.Last().Active = preset.One.Active;
			Waymarks.Last().Label = "1";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.Two.X;
			Waymarks.Last().Y = preset.Two.Y;
			Waymarks.Last().Z = preset.Two.Z;
			Waymarks.Last().ID = preset.Two.ID;
			Waymarks.Last().Active = preset.Two.Active;
			Waymarks.Last().Label = "2";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.Three.X;
			Waymarks.Last().Y = preset.Three.Y;
			Waymarks.Last().Z = preset.Three.Z;
			Waymarks.Last().ID = preset.Three.ID;
			Waymarks.Last().Active = preset.Three.Active;
			Waymarks.Last().Label = "3";

			Waymarks.Add( new ScratchWaymark() );
			Waymarks.Last().X = preset.Four.X;
			Waymarks.Last().Y = preset.Four.Y;
			Waymarks.Last().Z = preset.Four.Z;
			Waymarks.Last().ID = preset.Four.ID;
			Waymarks.Last().Active = preset.Four.Active;
			Waymarks.Last().Label = "4";
		}

		public WaymarkPreset GetPreset()
		{
			WaymarkPreset newPreset = new();

			newPreset.Name = Name;
			newPreset.MapID = MapID;

			newPreset.A.X = Waymarks[0].X;
			newPreset.A.Y = Waymarks[0].Y;
			newPreset.A.Z = Waymarks[0].Z;
			newPreset.A.ID = Waymarks[0].ID;
			newPreset.A.Active = Waymarks[0].Active;

			newPreset.B.X = Waymarks[1].X;
			newPreset.B.Y = Waymarks[1].Y;
			newPreset.B.Z = Waymarks[1].Z;
			newPreset.B.ID = Waymarks[1].ID;
			newPreset.B.Active = Waymarks[1].Active;

			newPreset.C.X = Waymarks[2].X;
			newPreset.C.Y = Waymarks[2].Y;
			newPreset.C.Z = Waymarks[2].Z;
			newPreset.C.ID = Waymarks[2].ID;
			newPreset.C.Active = Waymarks[2].Active;

			newPreset.D.X = Waymarks[3].X;
			newPreset.D.Y = Waymarks[3].Y;
			newPreset.D.Z = Waymarks[3].Z;
			newPreset.D.ID = Waymarks[3].ID;
			newPreset.D.Active = Waymarks[3].Active;

			newPreset.One.X = Waymarks[4].X;
			newPreset.One.Y = Waymarks[4].Y;
			newPreset.One.Z = Waymarks[4].Z;
			newPreset.One.ID = Waymarks[4].ID;
			newPreset.One.Active = Waymarks[4].Active;

			newPreset.Two.X = Waymarks[5].X;
			newPreset.Two.Y = Waymarks[5].Y;
			newPreset.Two.Z = Waymarks[5].Z;
			newPreset.Two.ID = Waymarks[5].ID;
			newPreset.Two.Active = Waymarks[5].Active;

			newPreset.Three.X = Waymarks[6].X;
			newPreset.Three.Y = Waymarks[6].Y;
			newPreset.Three.Z = Waymarks[6].Z;
			newPreset.Three.ID = Waymarks[6].ID;
			newPreset.Three.Active = Waymarks[6].Active;

			newPreset.Four.X = Waymarks[7].X;
			newPreset.Four.Y = Waymarks[7].Y;
			newPreset.Four.Z = Waymarks[7].Z;
			newPreset.Four.ID = Waymarks[7].ID;
			newPreset.Four.Active = Waymarks[7].Active;

			return newPreset;
		}

		public string Name = "";
		public UInt16 MapID = 0;
		public List<ScratchWaymark> Waymarks;
	}
}
