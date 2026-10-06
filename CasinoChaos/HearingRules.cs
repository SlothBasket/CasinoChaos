using System;

namespace GWYF_CasinoChaos
{
    internal static class HearingTuning
    {
        internal const float BadSideGain = 0.35f;
        internal const float BadSideCutoffHz = 2200f;
        internal const float BothMissingGain = 0.25f;
        internal const float BothMissingCutoffHz = 1200f;
        internal const float NormalCutoffHz = 22000f;
        internal const float MouthGain = 0.55f;
        internal const float MouthCutoffHz = 2200f;
        internal const float MouthDistortion = 0.08f;
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
            return !leftPresent ? Math.Max(0, -side) : !rightPresent ? Math.Max(0, side) : 0;
        }
        internal static void Parameters(bool bothMissing, float strength, out float gain, out float cutoff)
        {
            strength = Math.Max(0, Math.Min(1, strength));
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
