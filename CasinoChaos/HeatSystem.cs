using System.Collections.Generic;
using Extensions;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal static class HeatSystem
    {
        internal static int HeatPoints { get; private set; }
        internal static int HeatLevel => HeatRules.Level(HeatPoints);
        internal static PlayerController RecentOffender { get; private set; }
        // Disconnects do not erase global heat. Round reset clears attribution.
        internal sealed class Contribution
        {
            internal PlayerController Offender;
            internal PlayerProfile Profile;
            internal ulong SteamId;
            internal string Name;
            internal int Hits;
        }
        internal static readonly Dictionary<uint, Contribution> Contributions = new Dictionary<uint, Contribution>();
        private static readonly List<MafiaGuardController> Guards = new List<MafiaGuardController>();
        private static bool _wasServer, _wasCasino;

        internal static string Identity(PlayerController player)
        {
            if (!player) return "unavailable";
            var profile = player.GetComponent<PlayerProfile>();
            return $"'{(profile ? profile.playerName : player.name)}' netId={player.netId} steamId={(profile ? profile.steamId : 0)}";
        }
        internal static void AddCivilianHit(PlayerController player) => AddPoint(player, "Civilian hit");
        internal static void AddPoint(PlayerController player, string reason)
        {
            var game = NetworkSingleton<GameManager>.Instance;
            if (!NetworkServer.active || !player || !game || game.state != GameState.Game) return;
            int before = HeatPoints, oldLevel = HeatLevel;
            if (HeatPoints < int.MaxValue) HeatPoints++;
            RecentOffender = player;
            if (!Contributions.TryGetValue(player.netId, out var contribution))
            {
                var profile = player.GetComponent<PlayerProfile>();
                Contributions[player.netId] = contribution = new Contribution {
                    Offender = player, Profile = profile, SteamId = profile ? profile.steamId : 0,
                    Name = profile ? profile.playerName : player.name
                };
            }
            if (contribution.Hits < int.MaxValue) contribution.Hits++;
            Guards.RemoveAll(g => !g);
            int active = 0;
            foreach (var guard in Guards) if (guard.IsActiveResponse) active++;
            int requested = HeatRules.Reinforcements(oldLevel, HeatLevel, active), spawned = 0;
            // One bounded wave per tier transition. Defeats/disconnects never
            // schedule replacements. Failed spawns wait until the NEXT tier.
            for (int i = 0; i < requested; i++)
            {
                var guard = MafiaGuardSpawner.Spawn(RecentOffender, MafiaWeaponType.Gun);
                if (!guard) break;
                Guards.Add(guard); spawned++;
            }
            CasinoChaosPlugin.Log($"{reason}: offender={Identity(player)}; GlobalHeatPoints {before}->{HeatPoints}; " +
                $"HeatLevel {oldLevel}->{HeatLevel}; desired gun guards={HeatRules.Tiers[HeatLevel].DesiredGunGuards}; " +
                $"reinforcements spawned={spawned}/{requested}; active={active + spawned}");
            HeatNetwork.Publish();
        }
        internal static void Tick()
        {
            var game = NetworkSingleton<GameManager>.Instance;
            bool casino = NetworkServer.active && game && game.state == GameState.Game;
            if ((_wasServer && !NetworkServer.active) || (_wasCasino && !casino))
                Clear(NetworkServer.active ? "left casino game state" : "server stopped");
            _wasServer = NetworkServer.active; _wasCasino = casino;
        }
        internal static void Clear(string reason)
        {
            int points = HeatPoints, removed = 0;
            // Includes defeated corpses and guards removed from bookkeeping.
            foreach (var guard in Object.FindObjectsByType<MafiaGuardController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { guard.Despawn(reason); removed++; }
            HeatPoints = 0; RecentOffender = null; Contributions.Clear(); Guards.Clear();
            if (points != 0 || removed != 0)
                CasinoChaosPlugin.Log($"Heat reset: {reason}; GlobalHeatPoints {points}->0; HeatLevel {HeatRules.Level(points)}->0; previous-round guards removed={removed}");
            if (NetworkServer.active) { HeatSettings.ApplyForRound(); HeatNetwork.Publish(); }
        }
    }
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.InitializeScene))]
    internal static class HeatSceneInitialization
    {
        private static void Prefix(string sceneName)
        { if (NetworkServer.active) HeatSystem.Clear("GameManager.InitializeScene: " + sceneName); }
    }
    [HarmonyPatch(typeof(GameManager), "ServerRetrySameDay")]
    internal static class HeatDayRetry
    {
        private static void Prefix()
        { if (NetworkServer.active) HeatSystem.Clear("GameManager.ServerRetrySameDay"); }
    }
}
