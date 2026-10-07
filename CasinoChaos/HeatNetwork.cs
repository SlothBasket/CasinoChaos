using System.Collections.Generic;
using Mirror;

namespace GWYF_CasinoChaos
{
    internal struct HeatSnapshotRequest : NetworkMessage { internal byte Protocol; }
    internal struct HeatStateMessage : NetworkMessage { internal byte Protocol; internal int Points, Level; }
    internal static class HeatNetwork
    {
        private const byte Protocol = 1;
        private static bool _installed, _wasServer, _wasReady;
        private static NetworkConnectionToServer _client;
        private static readonly HashSet<NetworkConnectionToClient> Subscribers = new HashSet<NetworkConnectionToClient>();
        internal static int DisplayPoints { get; private set; }
        internal static int DisplayLevel { get; private set; }
        internal static void Install()
        {
            if (_installed) return;
            _installed = true;
            Writer<HeatSnapshotRequest>.write = (w,v) => w.WriteByte(v.Protocol);
            Reader<HeatSnapshotRequest>.read = r => new HeatSnapshotRequest { Protocol = r.ReadByte() };
            Writer<HeatStateMessage>.write = (w,v) => { w.WriteByte(v.Protocol); w.WriteInt(v.Points); w.WriteInt(v.Level); };
            Reader<HeatStateMessage>.read = r => new HeatStateMessage { Protocol = r.ReadByte(), Points = r.ReadInt(), Level = r.ReadInt() };
            RegisterReceiver();
        }
        internal static void RegisterReceiver()
        { if (_installed) NetworkClient.RegisterHandler<HeatStateMessage>(Receive, requireAuthentication: true); }
        private static HeatStateMessage Message() => new HeatStateMessage
        { Protocol = Protocol, Points = HeatSystem.HeatPoints, Level = HeatSystem.HeatLevel };
        private static void Snapshot(NetworkConnectionToClient conn, HeatSnapshotRequest request)
        {
            if (request.Protocol != Protocol || !conn.isReady) return;
            Subscribers.Add(conn); conn.Send(Message());
        }
        private static void Receive(HeatStateMessage state)
        {
            if (state.Protocol != Protocol || state.Points < 0 || state.Level < 0 || state.Level > 5) return;
            DisplayPoints = state.Points; DisplayLevel = state.Level;
        }
        internal static void Publish()
        {
            if (!_installed || !NetworkServer.active) return;
            var state = Message(); Receive(state);
            // Only explicit subscribers receive custom messages. Vanilla clients
            // receive no unknown heat message IDs; gameplay stays on the host.
            foreach (var conn in Subscribers)
                if (conn.isReady && conn.isAuthenticated) conn.Send(state);
        }
        internal static void Tick()
        {
            if (!_installed) return;
            if (NetworkServer.active && !_wasServer)
            {
                Subscribers.Clear();
                NetworkServer.RegisterHandler<HeatSnapshotRequest>(Snapshot, requireAuthentication: true);
                HeatSystem.Clear("server started");
            }
            if (!NetworkServer.active && _wasServer) Subscribers.Clear();
            _wasServer = NetworkServer.active;
            Subscribers.RemoveWhere(c => !NetworkServer.connections.TryGetValue(c.connectionId, out var live) || !ReferenceEquals(c, live));
            if (_client != NetworkClient.connection || !NetworkClient.active)
            { _client = null; _wasReady = false; DisplayPoints = DisplayLevel = 0; HeatHud.ResetFeedback(); }
            if (!NetworkClient.ready) _wasReady = false;
            if (NetworkClient.active && NetworkClient.isConnected && NetworkClient.ready && (_client == null || !_wasReady))
            {
                RegisterReceiver(); _client = NetworkClient.connection; _wasReady = true;
                _client.Send(new HeatSnapshotRequest { Protocol = Protocol });
            }
        }
        internal static void Shutdown()
        {
            NetworkClient.UnregisterHandler<HeatStateMessage>(); NetworkServer.UnregisterHandler<HeatSnapshotRequest>();
            _installed = _wasServer = _wasReady = false; _client = null; Subscribers.Clear(); DisplayPoints = DisplayLevel = 0;
            HeatHud.Shutdown();
        }
    }
}
