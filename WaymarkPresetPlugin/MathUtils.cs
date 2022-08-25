using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace WaymarkPresetPlugin
{
    internal static class MathUtils
    {
        public static Vector3[] ComputeRadialPositions(Vector3 center, float radius_Yalms, int numPoints, float angleOffset_Deg = 0f)
        {
            //	Can't have less than one point (even that doesn't make much sense, but it's technically allowable).
            numPoints = Math.Max(1, numPoints);
            var computedPoints = new Vector3[numPoints];

            //	Zero azimuth is facing North (90 degrees)
            angleOffset_Deg -= 90f;
            double stepAngle_Deg = 360.0 / numPoints;

            //	Compute the coordinates on the circle about the center point.
            for (int i = 0; i < numPoints; ++i)
            {
                //	Because of FFXIV's coordinate system, we need to go backward in angle.
                double angle_Rad = (i * stepAngle_Deg + angleOffset_Deg) * Math.PI / 180.0;
                computedPoints[i].X = (float)Math.Cos(angle_Rad);
                computedPoints[i].Z = (float)Math.Sin(angle_Rad);
                computedPoints[i] *= radius_Yalms;
                computedPoints[i] += center;
            }

            return computedPoints;
        }

        public static Vector3[] ComputeSquarePositions(Vector3 center, float s)
        {
            return new Vector3[]
            {
                center + new Vector3( 0, 0, -s),
                center + new Vector3( s, 0, -s),
                center + new Vector3( s, 0,  0),
                center + new Vector3( s, 0,  s),
                center + new Vector3( 0, 0,  s),
                center + new Vector3(-s, 0,  s),
                center + new Vector3(-s, 0,  0),
                center + new Vector3(-s, 0, -s),
            };
        }

        public static int Mod2(int a, int b) {
            if (a == 0)
            {
                return 0;
            }
            else if (a % b == 0)
            {
                return b;
            }
            else
            {
                return a % b;
            }
        }
    }
}
