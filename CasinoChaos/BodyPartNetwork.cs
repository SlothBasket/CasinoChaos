using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
#if DEBUG
using UnityEngine.InputSystem;
using Extensions;
#endif

namespace GWYF_CasinoChaos
{
    internal struct BodySnapshotRequest : NetworkMessage { internal byte Protocol; }
    internal struct BodyStateMessage : NetworkMessage
    { internal byte Protocol; internal ulong SteamId; internal byte Mask; internal uint Revision; }

    internal static class BodyPartNetwork
    {
        private const byte Protocol = 3;
        private static readonly Dictionary<ulong, BodyState> ServerStates = new Dictionary<ulong, BodyState>();
        private static bool _wasServer, _installed;
        private static NetworkConnectionToServer _client;
#if DEBUG
        private static ulong _debugTarget;
#endif
        internal static void Install()
        {
            if (_installed) return;
            _installed = true;
            // This project is not Mirror-weaved. Register both readers/writers
            // explicitly; do not add NetworkBehaviours to vanilla player prefabs.
            Writer<BodySnapshotRequest>.write = (w, v) => w.WriteByte(v.Protocol);
            Reader<BodySnapshotRequest>.read = r => new BodySnapshotRequest { Protocol = r.ReadByte() };
            Writer<BodyStateMessage>.write = (w, v) =>
            { w.WriteByte(v.Protocol); w.WriteULong(v.SteamId); w.WriteByte(v.Mask); w.WriteUInt(v.Revision); };
            Reader<BodyStateMessage>.read = r => new BodyStateMessage
            { Protocol = r.ReadByte(), SteamId = r.ReadULong(), Mask = r.ReadByte(), Revision = r.ReadUInt() };
            BodyPartState.Changed += LogChange;
            RegisterClientReceiver();
#if DEBUG
            CasinoChaosPlugin.Log("DEVELOPMENT ONLY, host: [ LeftEar, ] RightEar, F8 Mouth; Ctrl+Shift+Home LeftLeg, Ctrl+Shift+End RightLeg; F10 selects a player (default local host). Clients cannot change ears.");
#endif
        }
        internal static void RegisterClientReceiver()
        {
            if (_installed) NetworkClient.RegisterHandler<BodyStateMessage>(Receive, requireAuthentication: true);
        }
        private static void LogChange(ulong id, CustomBodyPart part, bool present) =>
            CasinoChaosPlugin.Log($"Body state: steamId={id} {part} {(present ? "restored/present" : "removed")}");
        private static BodyStateMessage Message(ulong id, BodyState value) => new BodyStateMessage
        { Protocol = Protocol, SteamId = id, Mask = value.PresentMask, Revision = value.Revision };
        private static void Snapshot(NetworkConnectionToClient conn, BodySnapshotRequest request)
        {
            if (request.Protocol != Protocol) { CasinoChaosPlugin.Log("Body snapshot refused: incompatible protocol."); return; }
            foreach (var entry in ServerStates) conn.Send(Message(entry.Key, entry.Value));
            EarMachineNetwork.Snapshot(conn);
        }
        private static void Receive(BodyStateMessage message)
        {
            if (message.Protocol == Protocol) BodyPartState.Receive(message.SteamId, message.Mask, message.Revision);
        }
        internal static void Tick()
        {
            if (!_installed) return;
            if (NetworkServer.active && !_wasServer)
            {
                ServerStates.Clear(); BodyPartState.Clear();
                NetworkServer.RegisterHandler<BodySnapshotRequest>(Snapshot, requireAuthentication: true);
            }
            if (!NetworkServer.active && _wasServer) ServerStates.Clear();
            _wasServer = NetworkServer.active;
            if (_client != NetworkClient.connection || (!NetworkClient.active && _client != null))
            { _client = null; BodyPartState.Clear(); }
            if (NetworkClient.active && NetworkClient.isConnected && _client == null)
            {
                NetworkClient.RegisterHandler<BodyStateMessage>(Receive, requireAuthentication: true);
                // Player identity is populated after authentication. Request
                // once ready, so the authenticated handler will not drop it.
                if (NetworkClient.ready)
                { _client = NetworkClient.connection; _client.Send(new BodySnapshotRequest { Protocol = Protocol }); }
            }
            if (NetworkServer.active)
                foreach (var conn in NetworkServer.connections.Values)
                {
                    if (!conn.isAuthenticated || !conn.identity) continue;
                    var profile = conn.identity.GetComponent<PlayerProfile>();
                    if (!profile || profile.steamId == 0 || ServerStates.ContainsKey(profile.steamId)) continue;
                    var state = new BodyState(BodyState.CompleteMask, 1);
                    ServerStates.Add(profile.steamId, state);
                    BodyPartState.Receive(profile.steamId, state.PresentMask, state.Revision);
                    NetworkServer.SendToAll(Message(profile.steamId, state), sendToReadyOnly: true);
                }
#if DEBUG
            DevelopmentControls();
#endif
        }
        internal static bool ServerSet(ulong steamId, CustomBodyPart part, bool present)
        {
            if (!NetworkServer.active || !ServerStates.TryGetValue(steamId, out var before)) return false;
            if ((byte)part > (byte)CustomBodyPart.Butt) return false;
            if (before.Has(part) == present) return true;
            byte mask = present ? (byte)(before.PresentMask | (1 << (int)part)) : (byte)(before.PresentMask & ~(1 << (int)part));
            var state = new BodyState(mask, before.Revision + 1);
            ServerStates[steamId] = state;
            BodyPartState.Receive(steamId, mask, state.Revision);
            NetworkServer.SendToAll(Message(steamId, state), sendToReadyOnly: true);
            return true;
        }
        internal static bool TryGetServerState(ulong steamId, out BodyState state)
        {
            state = BodyState.Complete;
            return NetworkServer.active && ServerStates.TryGetValue(steamId, out state);
        }
#if DEBUG
        private static bool DevelopmentPressed(KeyCode key)
        {
            // Installed PlayerSettings.activeInputHandler=1 (Input System only).
            // Keep the requested KeyCode binding without invoking legacy Input.
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (key == KeyCode.LeftBracket) return keyboard.leftBracketKey.wasPressedThisFrame;
            if (key == KeyCode.RightBracket) return keyboard.rightBracketKey.wasPressedThisFrame;
            return false;
        }
        private static void DevelopmentControls()
        {
            if (!NetworkServer.active || !NetworkClient.localPlayer) return;

            // No per-frame roster allocation unless a debug key was actually used.
            var k = Keyboard.current;
            bool left = DevelopmentPressed(KeyCode.LeftBracket), right = DevelopmentPressed(KeyCode.RightBracket);
            bool mouth = k != null && k.f8Key.wasPressedThisFrame, select = k != null && k.f10Key.wasPressedThisFrame;
            bool chord = k != null && (k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed) && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
            bool leftLeg = chord && k.homeKey.wasPressedThisFrame, rightLeg = chord && k.endKey.wasPressedThisFrame;
            if (!left && !right && !mouth && !select && !leftLeg && !rightLeg) return;
            var players = new List<PlayerProfile>();
            foreach (var conn in NetworkServer.connections.Values)
                if (conn.identity && conn.identity.TryGetComponent<PlayerProfile>(out var profile) && profile.steamId != 0) players.Add(profile);
            players.Sort((a,b) => a.steamId.CompareTo(b.steamId));
            var local = NetworkClient.localPlayer.GetComponent<PlayerProfile>();
            if (_debugTarget == 0 && local) _debugTarget = local.steamId;
            int index = players.FindIndex(p => p.steamId == _debugTarget);
            if (players.Count == 0) return;
            if (select) index = (index + 1) % players.Count;
            if (index < 0) index = 0;
            var selected = players[index]; _debugTarget = selected.steamId;
            CasinoChaosPlugin.Log($"DEVELOPMENT target: '{selected.playerName}' steamId={_debugTarget}");
            if (left) ServerSet(_debugTarget, CustomBodyPart.LeftEar, !BodyPartState.Get(_debugTarget).Has(CustomBodyPart.LeftEar));
            if (right) ServerSet(_debugTarget, CustomBodyPart.RightEar, !BodyPartState.Get(_debugTarget).Has(CustomBodyPart.RightEar));
            if (leftLeg) ServerSet(_debugTarget, CustomBodyPart.LeftLeg, !BodyPartState.Get(_debugTarget).Has(CustomBodyPart.LeftLeg));
            if (rightLeg) ServerSet(_debugTarget, CustomBodyPart.RightLeg, !BodyPartState.Get(_debugTarget).Has(CustomBodyPart.RightLeg));
            if (mouth)
            {
                var organs = selected.GetComponent<PlayerOrgans>();
                var manager = NetworkSingleton<OrganManager>.Instance;
                var state = manager ? manager.GetOrganData(organs) : null;
                if (state != null)
                { manager.ServerToggleOrgan(organs, OrganType.Mouth, !state.mouth); CasinoChaosPlugin.Log("DEVELOPMENT vanilla Mouth toggled on host."); }
            }
        }
#endif
        internal static void Shutdown()
        {
            NetworkClient.UnregisterHandler<BodyStateMessage>(); NetworkServer.UnregisterHandler<BodySnapshotRequest>();
            _client = null; _wasServer = false; _installed = false; ServerStates.Clear();
            BodyPartState.Clear(); BodyPartState.Changed -= LogChange;
#if DEBUG
            _debugTarget = 0;
#endif
        }
    }
    [HarmonyLib.HarmonyPatch(typeof(NetworkClient), "Initialize")]
    internal static class BodyClientInitialization
    {
        private static void Postfix()
        { BodyPartNetwork.RegisterClientReceiver(); EarMachineNetwork.RegisterClientReceiver(); LegMovement.RegisterReceiver(); HeatNetwork.RegisterReceiver(); DongAppearanceNetwork.RegisterReceiver(); FartNetwork.RegisterReceiver(); }
    }
}
