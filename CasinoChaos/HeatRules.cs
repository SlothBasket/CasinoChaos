namespace GWYF_CasinoChaos
{
    internal readonly struct HeatTier
    {
        internal readonly int MinimumPoints, DesiredGunGuards;
        internal HeatTier(int minimumPoints, int desiredGunGuards)
        { MinimumPoints = minimumPoints; DesiredGunGuards = desiredGunGuards; }
    }
    internal static class HeatRules
    {
        // Prototype balancing is centralized; no tier values belong in guard AI.
        internal static readonly HeatTier[] Tiers = {
            new HeatTier(0, 0), new HeatTier(3, 1), new HeatTier(6, 2),
            new HeatTier(10, 3), new HeatTier(15, 5), new HeatTier(21, 7)
        };
        internal static int Level(int points)
        {
            for (int i = Tiers.Length - 1; i > 0; i--)
                if (points >= Tiers[i].MinimumPoints) return i;
            return 0;
        }
        internal static int Reinforcements(int oldLevel, int newLevel, int surviving)
            => newLevel > oldLevel ? System.Math.Max(0, Tiers[newLevel].DesiredGunGuards - surviving) : 0;
    }
}
