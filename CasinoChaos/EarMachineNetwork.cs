using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal struct EarMachinePress : NetworkMessage { internal uint Machine, Sequence; internal byte Entry; }
    internal struct EarMachineView : NetworkMessage { internal uint Machine; internal int EyePrice; internal bool Buying; }
    internal struct EarMachineFeedback : NetworkMessage { internal uint Machine, Player; internal byte Entry; }

    internal static class EarMachineNetwork
    {
        private static readonly EarPressLedger Sequences = new EarPressLedger();
        private static readonly Dictionary<uint, EarMachineView> Views = new Dictionary<uint, EarMachineView>();
        private static NetworkConnectionToServer _client;
        private static uint _sequence;
        private static bool _installed, _wasServer, _seeded;
        internal static void Install()
        {
            if (_installed) return;
            _installed = true;
            Writer<EarMachinePress>.write = (w,v) => { w.WriteUInt(v.Machine); w.WriteUInt(v.Sequence); w.WriteByte(v.Entry); };
            Reader<EarMachinePress>.read = r => new EarMachinePress { Machine = r.ReadUInt(), Sequence = r.ReadUInt(), Entry = r.ReadByte() };
            Writer<EarMachineView>.write = (w,v) => { w.WriteUInt(v.Machine); w.WriteInt(v.EyePrice); w.WriteBool(v.Buying); };
            Reader<EarMachineView>.read = r => new EarMachineView { Machine = r.ReadUInt(), EyePrice = r.ReadInt(), Buying = r.ReadBool() };
            Writer<EarMachineFeedback>.write = (w,v) => { w.WriteUInt(v.Machine); w.WriteUInt(v.Player); w.WriteByte(v.Entry); };
            Reader<EarMachineFeedback>.read = r => new EarMachineFeedback { Machine = r.ReadUInt(), Player = r.ReadUInt(), Entry = r.ReadByte() };
            RegisterClientReceiver();
        }
        internal static void RegisterClientReceiver()
        {
            if (!_installed) return;
            NetworkClient.RegisterHandler<EarMachineView>(ReceiveView, requireAuthentication: true);
            NetworkClient.RegisterHandler<EarMachineFeedback>(Feedback, requireAuthentication: true);
        }
        internal static void Tick()
        {
            if (!_installed) return;
            if (NetworkServer.active && !_wasServer)
            {
                Sequences.Clear();
                NetworkServer.RegisterHandler<EarMachinePress>(Press, requireAuthentication: true);
            }
            if (!NetworkServer.active && _wasServer) Sequences.Clear();
            _wasServer = NetworkServer.active;
            if (_client != NetworkClient.connection)
            { _client = NetworkClient.connection; _sequence = 0; Views.Clear(); RegisterClientReceiver(); }
            if (!_seeded && (NetworkClient.active || NetworkServer.active))
            {
                _seeded = true;
                foreach (var machine in Object.FindObjectsByType<BodyShreddingMachine>(FindObjectsSortMode.None))
                    if (machine.netId != 0) EarMachineController.Ensure(machine);
            }
            if (!NetworkClient.active && !NetworkServer.active) _seeded = false;
        }
        internal static void SendPress(EarMachineController machine, MachinePart entry)
        {
            if (!_installed || !NetworkClient.ready || !NetworkClient.localPlayer || !machine) return;
            if (_client != NetworkClient.connection) { _client = NetworkClient.connection; _sequence = 0; }
            // One path for host and remote operator alike. No local server call.
            _client.Send(new EarMachinePress { Machine = machine.Machine.netId, Sequence = ++_sequence, Entry = (byte)entry });
        }
        private static void Press(NetworkConnectionToClient conn, EarMachinePress request)
        {
            if (!conn.identity || !Sequences.TryAccept(conn, request.Sequence)) return;
            if (!NetworkServer.spawned.TryGetValue(request.Machine, out var identity)) return;
            var machine = identity.GetComponent<BodyShreddingMachine>();
            var controller = machine ? EarMachineController.Ensure(machine) : null;
            var player = conn.identity.GetComponent<PlayerInteract>();
            if (!controller || !controller.CanPress(player, (MachinePart)request.Entry)) return;
            NetworkServer.SendToAll(new EarMachineFeedback { Machine = request.Machine, Player = conn.identity.netId, Entry = request.Entry }, sendToReadyOnly: true);
            controller.ServerTrade(player, (MachinePart)request.Entry);
        }
        private static void ReceiveView(EarMachineView view)
        {
            Views[view.Machine] = view;
            if (NetworkClient.spawned.TryGetValue(view.Machine, out var identity))
            {
                var machine = identity.GetComponent<BodyShreddingMachine>();
                if (machine) EarMachineController.Ensure(machine)?.ApplyView(view);
            }
        }
        internal static void ApplyPending(EarMachineController controller)
        {
            if (Views.TryGetValue(controller.Machine.netId, out var view)) controller.ApplyView(view);
        }
        private static void Feedback(EarMachineFeedback message)
        {
            if (!NetworkClient.spawned.TryGetValue(message.Machine, out var identity)) return;
            var controller = identity.GetComponent<EarMachineController>();
            PlayerInteract player = null;
            if (NetworkClient.spawned.TryGetValue(message.Player, out var actor)) player = actor.GetComponent<PlayerInteract>();
            if (controller) controller.PlayFeedback(player, (MachinePart)message.Entry);
        }
        internal static void Publish(EarMachineController controller)
        {
            if (NetworkServer.active && controller && controller.Initialized)
                NetworkServer.SendToAll(controller.ServerView(), sendToReadyOnly: true);
        }
        internal static void Snapshot(NetworkConnectionToClient conn)
        {
            foreach (var controller in Object.FindObjectsByType<EarMachineController>(FindObjectsSortMode.None))
                if (controller.Initialized) conn.Send(controller.ServerView());
        }
        internal static void Shutdown()
        {
            _installed = false;
            NetworkServer.UnregisterHandler<EarMachinePress>();
            NetworkClient.UnregisterHandler<EarMachineView>(); NetworkClient.UnregisterHandler<EarMachineFeedback>();
            Sequences.Clear(); Views.Clear(); _client = null; _sequence = 0; _seeded = _wasServer = false;
            foreach (var controller in Object.FindObjectsByType<EarMachineController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            { controller.Restore(true); Object.Destroy(controller); }
        }
    }
}
