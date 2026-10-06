using System;

namespace GWYF_CasinoChaos
{
    internal static class GunSpreadRules
    {
        // Uniform solid angle inside a cone. Inputs are independent [0,1]
        // samples; output is a unit vector about the local +Z aim axis.
        internal static void Sample(float degrees, float radialSample, float azimuthSample,
            out float x, out float y, out float z)
        {
            double cos = 1 + (Math.Cos(degrees * Math.PI / 180) - 1) * radialSample;
            double sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
            double angle = azimuthSample * Math.PI * 2;
            x = (float)(sin * Math.Cos(angle));
            y = (float)(sin * Math.Sin(angle));
            z = (float)cos;
        }
    }
}
