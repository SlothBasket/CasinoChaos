using System;

namespace GWYF_CasinoChaos
{
    internal static class HearingTuning
    {
        internal static float BadSideGain = 0.65f;
        internal static float BadSideCutoffHz = 750f;
        internal static float BothMissingGain = 0.55f;
        internal static float BothMissingCutoffHz = 550f;
        internal static float DirectionExponent = 1.25f;
        internal const float NormalCutoffHz = 22000f;
        internal static float MouthGain = 0.60f;
        internal static float MouthCutoffHz = 650f;
        internal static float MouthDistortion = 0.15f;
        internal const float DirectionSmoothingSeconds = 0.08f;
        internal const float AudioTickSeconds = 0.02f;
        internal const float UnitySourceRefreshSeconds = 0.5f;
    }
    internal static class HearingRules
    {
        internal static float Strength(bool leftPresent, bool rightPresent, float side)
        {
            if (!leftPresent && !rightPresent) return 1f;
            side = Math.Max(-1, Math.Min(1, side));
            return !leftPresent ? Math.Max(0, (-side+.25f)/1.25f) : !rightPresent ? Math.Max(0, (side+.25f)/1.25f) : 0;
        }
        internal static void Parameters(bool bothMissing, float strength, out float gain, out float cutoff)
        {
            strength = Math.Max(0, Math.Min(1, strength));
            strength = bothMissing ? strength : (float)Math.Pow(strength, HearingTuning.DirectionExponent);
            gain = 1 + ((bothMissing ? HearingTuning.BothMissingGain : HearingTuning.BadSideGain) - 1) * strength;
            // Log interpolation avoids staying bright until the extreme side.
            cutoff = (float)(HearingTuning.NormalCutoffHz * Math.Pow(
                (bothMissing ? HearingTuning.BothMissingCutoffHz : HearingTuning.BadSideCutoffHz) / HearingTuning.NormalCutoffHz, strength));
        }
        internal static bool IsWorldEvent(string path) => !string.IsNullOrEmpty(path)
            && path.IndexOf("music", StringComparison.OrdinalIgnoreCase) < 0
            && !path.StartsWith("event:/UI", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("event:/GenericUI", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("event:/Menu", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("event:/System", StringComparison.OrdinalIgnoreCase);
    }
}
