using System;

namespace GWYF_CasinoChaos
{
    internal static class BatStrikeRules
    {
        // Check at impact time against the direction committed at wind-up.
        internal static bool CanReach(float planarDistance, float heightDifference, float angleDegrees, float attackRange)
            => planarDistance <= attackRange && Math.Abs(heightDifference) <= 2f && angleDegrees <= 50f;
    }
}
