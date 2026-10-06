using System.Collections.Generic;
using Extensions;
using Mirror;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    // netId is scoped to the current server scene. Scene changes, disconnects,
    // server stop and unload discard records, so a new identity cannot inherit heat.
    internal static class HeatSystem
    {
        private sealed class Record
        {
            internal PlayerController Offender;
            internal int Heat;
            internal bool Responded;
            internal MafiaGuardController Guard;
            internal float NextSpawnAttempt;
        }

        private static readonly Dictionary<uint, Record> Records = new Dictionary<uint, Record>();
        private static readonly List<uint> Invalid = new List<uint>();
        private static readonly HeatResponseTier FirstResponse = new HeatResponseTier(3, 1);

        internal static string Identity(PlayerController player)
        {
            var profile = player.GetComponent<PlayerProfile>();
            return $"'{(profile ? profile.playerName : player.name)}' netId={player.netId} steamId={(profile ? profile.steamId : 0)}";
        }

        private static Record Get(PlayerController player)
        {
            if (!Records.TryGetValue(player.netId, out var record))
                Records[player.netId] = record = new Record { Offender = player };
            return record;
        }

        internal static void AddCivilianHit(PlayerController player)
        {
            if (!NetworkServer.active) return;
            var record = Get(player);
            int before = record.Heat;
            record.Heat++;
            CasinoChaosPlugin.Log($"Civilian hit: offender={Identity(player)} heat={before}->{record.Heat}");
            TryRespond(record);
        }

        internal static void SetDevelopmentHeat(PlayerController player, int value)
        {
            if (!NetworkServer.active) return;
            var record = Get(player);
            int before = record.Heat;
            record.Heat = value;
            CasinoChaosPlugin.Log($"DEVELOPMENT ONLY: offender={Identity(player)} heat={before}->{value}");
            TryRespond(record);
        }

        private static void TryRespond(Record record)
        {
            if (record.Responded || record.Heat < FirstResponse.MinimumHeat || Time.time < record.NextSpawnAttempt)
                return;
            if (!NetworkSingleton<GameManager>.Instance || NetworkSingleton<GameManager>.Instance.state != GameState.Game)
                return;
            record.NextSpawnAttempt = Time.time + 3f;
            record.Guard = MafiaGuardSpawner.Spawn(record.Offender);
            if (record.Guard) record.Responded = true;
        }

        internal static void Tick()
        {
            Invalid.Clear();
            foreach (var pair in Records)
            {
                var record = pair.Value;
                if (!record.Offender || record.Offender.connectionToClient == null ||
                    !NetworkServer.spawned.ContainsKey(pair.Key))
                {
                    // A defeated corpse owns its timer independently of heat.
                    if (record.Guard && !record.Guard.IsDefeated) record.Guard.Despawn("offender disconnected");
                    Invalid.Add(pair.Key);
                }
                else TryRespond(record);
            }
            foreach (uint key in Invalid) Records.Remove(key);
        }

        internal static void Clear(string reason)
        {
            foreach (var record in Records.Values)
                if (record.Guard) record.Guard.Despawn(reason);
            Records.Clear();
        }
    }

    // Only the first response exists. These fields leave room for future tiers,
    // without spawning gunmen or enabling any special weapon behavior.
    internal sealed class HeatResponseTier
    {
        internal readonly int MinimumHeat;
        internal readonly int BatGuardCount;
        internal readonly int GunGuardCount = 0;
        internal readonly float GunAccuracy = 0f;
        internal readonly bool SpecialWeaponBehavior = false;
        internal HeatResponseTier(int minimumHeat, int batGuardCount)
        { MinimumHeat = minimumHeat; BatGuardCount = batGuardCount; }
    }
}
