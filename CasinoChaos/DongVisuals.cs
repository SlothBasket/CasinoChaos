using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal static class DongVisuals
    {
        private static readonly Dictionary<PlayerController,DongVisualController> Players = new Dictionary<PlayerController,DongVisualController>();
        private static readonly List<PlayerController> Removed = new List<PlayerController>();
        private static readonly HashSet<PlayerController> Failed = new HashSet<PlayerController>();
        private static bool _installed;
        private static float _nextScan;
        internal static void Install()
        {
            if (_installed) return;
            _installed = true; _nextScan = 0;
            BodyPartState.Changed += Changed;
        }
        private static void Changed(ulong id, CustomBodyPart part, bool present)
        {
            foreach (var pair in Players)
            {
                var profile = pair.Key ? pair.Key.GetComponent<PlayerProfile>() : null;
                if (profile && profile.steamId == id && pair.Value) Refresh(pair.Key,pair.Value);
            }
        }
        internal static void Tick()
        {
            if (!_installed || Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + .25f;
            if (!NetworkClient.active) { if (Players.Count != 0) Clear(); return; }
            Removed.Clear();
            foreach (var pair in Players)
                if (!pair.Key || !pair.Value || pair.Value.Disposed ||
                    !NetworkClient.spawned.TryGetValue(pair.Key.netId, out var identity) || identity.gameObject != pair.Key.gameObject)
                {
                    if (pair.Value) { pair.Value.Dispose(); UnityEngine.Object.Destroy(pair.Value); }
                    Removed.Add(pair.Key);
                }
            foreach (var player in Removed) Players.Remove(player);
            Failed.RemoveWhere(p => !p);
            foreach (var identity in NetworkClient.spawned.Values)
            {
                if (!identity) continue;
                var player = identity.GetComponent<PlayerController>();
                if (!player || Failed.Contains(player)) continue;
                if (!Players.TryGetValue(player,out var visual))
                {
                    visual = player.GetComponent<DongVisualController>();
                    // A controller disposed this frame is destroyed at frame end.
                    if (visual && visual.Disposed) continue;
                    if (!visual) visual = player.gameObject.AddComponent<DongVisualController>();
                    try { visual.Initialize(player); Players.Add(player,visual); }
                    catch (Exception e)
                    {
                        visual.Dispose(); UnityEngine.Object.Destroy(visual); Failed.Add(player);
                        CasinoChaosPlugin.Log("Dong visual setup failed for " + HeatSystem.Identity(player) + ": " + e);
                    }
                }
                else Refresh(player,visual);
            }
        }
        internal static void RefreshAppearance(ulong id)
        {
            foreach (var pair in Players)
            {
                var profile=pair.Key?pair.Key.GetComponent<PlayerProfile>():null;
                if(profile&&profile.steamId==id&&pair.Value)Refresh(pair.Key,pair.Value);
            }
        }
        internal static void MarkFailed(PlayerController player) => Failed.Add(player);
        private static void Refresh(PlayerController player, DongVisualController visual)
        {
            try { visual.RefreshState(); }
            catch (Exception e)
            {
                visual.Dispose(); Failed.Add(player);
                CasinoChaosPlugin.Log("Dong visual refresh failed for " + HeatSystem.Identity(player) + ": " + e);
            }
        }
        internal static void Clear()
        {
            foreach (var visual in UnityEngine.Object.FindObjectsByType<DongVisualController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            { visual.Dispose(); UnityEngine.Object.Destroy(visual); }
            Players.Clear(); Removed.Clear(); Failed.Clear(); _nextScan = 0;
        }
        internal static void Shutdown()
        {
            if (!_installed) return;
            _installed = false; BodyPartState.Changed -= Changed; Clear();
        }
    }
}
