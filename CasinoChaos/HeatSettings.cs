using BepInEx.Configuration;

namespace GWYF_CasinoChaos
{
    internal static class HeatSettings
    {
        private static readonly ConfigEntry<int>[] Minimums = new ConfigEntry<int>[5], Guards = new ConfigEntry<int>[5];
        private static bool _bound;
        internal static void Bind(ConfigFile config)
        {
            if (_bound) return;
            _bound = true;
            for (int i = 0; i < 5; i++)
            {
                var defaults = HeatRules.Tiers[i + 1];
                Minimums[i] = config.Bind("Heat", $"Level{i + 1}MinimumPoints", defaults.MinimumPoints,
                    new ConfigDescription("Host only; applied on next round/reset. Thresholds must strictly increase.", new AcceptableValueRange<int>(1, int.MaxValue)));
                Guards[i] = config.Bind("Heat", $"Level{i + 1}GunGuards", defaults.DesiredGunGuards,
                    new ConfigDescription("Desired surviving gun guard count on tier entry. Applied on next round/reset.", new AcceptableValueRange<int>(0, 30)));
            }
        }
        internal static void ApplyForRound()
        {
            if (!_bound) return;
            int previous = 0;
            for (int i = 0; i < 5; i++)
            {
                if (Minimums[i].Value <= previous)
                { CasinoChaosPlugin.Log("Heat configuration rejected: thresholds must strictly increase; retaining last valid tiers."); return; }
                previous = Minimums[i].Value;
            }
            for (int i = 0; i < 5; i++) HeatRules.Tiers[i + 1] = new HeatTier(Minimums[i].Value, Guards[i].Value);
        }
    }
}
