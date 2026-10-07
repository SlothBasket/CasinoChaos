using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GWYF_CasinoChaos
{
    internal struct DongAppearanceRequest : NetworkMessage { internal byte Protocol; }
    internal struct DongShuffleRequest : NetworkMessage { internal byte Protocol; }
    internal struct DongAppearanceMessage : NetworkMessage { internal byte Protocol; internal ulong Player; internal uint Shuffle; }
    internal static class DongAppearanceNetwork
    {
        private const byte Protocol=1;
        private static bool _installed,_wasServer,_wasReady;
        private static NetworkConnectionToServer _client;
        private static readonly Dictionary<ulong,uint> ServerRolls=new Dictionary<ulong,uint>(), ClientRolls=new Dictionary<ulong,uint>();
        private static readonly HashSet<NetworkConnectionToClient> Subscribers=new HashSet<NetworkConnectionToClient>();
        private static readonly Dictionary<NetworkConnectionToClient,float> LastPress=new Dictionary<NetworkConnectionToClient,float>();
        internal static uint Roll(ulong id)
        { var rolls=NetworkServer.active?ServerRolls:ClientRolls;return rolls.TryGetValue(id,out var n)?n:0; }
        internal static void Install()
        {
            if(_installed)return;_installed=true;
            Writer<DongAppearanceRequest>.write=(w,v)=>w.WriteByte(v.Protocol);
            Reader<DongAppearanceRequest>.read=r=>new DongAppearanceRequest{Protocol=r.ReadByte()};
            Writer<DongShuffleRequest>.write=(w,v)=>w.WriteByte(v.Protocol);
            Reader<DongShuffleRequest>.read=r=>new DongShuffleRequest{Protocol=r.ReadByte()};
            Writer<DongAppearanceMessage>.write=(w,v)=>{w.WriteByte(v.Protocol);w.WriteULong(v.Player);w.WriteUInt(v.Shuffle);};
            Reader<DongAppearanceMessage>.read=r=>new DongAppearanceMessage{Protocol=r.ReadByte(),Player=r.ReadULong(),Shuffle=r.ReadUInt()};
            RegisterReceiver();
        }
        internal static void RegisterReceiver()
        {if(_installed)NetworkClient.RegisterHandler<DongAppearanceMessage>(Receive,requireAuthentication:true);}
        private static void Receive(DongAppearanceMessage message)
        {
            if(message.Protocol!=Protocol||message.Player==0)return;
            ClientRolls[message.Player]=message.Shuffle;
            DongVisuals.RefreshAppearance(message.Player);
        }
        private static void Snapshot(NetworkConnectionToClient conn,DongAppearanceRequest request)
        {
            if(request.Protocol!=Protocol||!conn.isReady)return;
            Subscribers.Add(conn);
            foreach(var entry in ServerRolls)conn.Send(new DongAppearanceMessage{Protocol=Protocol,Player=entry.Key,Shuffle=entry.Value});
        }
        private static void Shuffle(NetworkConnectionToClient conn,DongShuffleRequest request)
        {
            if(request.Protocol!=Protocol||!Subscribers.Contains(conn)||!conn.isReady||!conn.identity)return;
            var profile=conn.identity.GetComponent<PlayerProfile>();
            if(!profile||profile.steamId==0||!BodyPartNetwork.TryGetServerState(profile.steamId,out var state)||!state.Has(CustomBodyPart.Dong))return;
            if(LastPress.TryGetValue(conn,out var last)&&Time.unscaledTime-last<.3f)return;
            LastPress[conn]=Time.unscaledTime;
            ServerReroll(profile.steamId);
            CasinoChaosPlugin.Log($"Dong size shuffled: '{profile.playerName}' steamId={profile.steamId}; server=true");
        }
        // Called only after an accepted host transaction or authenticated shuffle.
        internal static void ServerReroll(ulong player)
        {
            if(!_installed||!NetworkServer.active||player==0)return;
            ServerRolls.TryGetValue(player,out uint before);
            uint roll=unchecked(before+1);ServerRolls[player]=roll;
            var message=new DongAppearanceMessage{Protocol=Protocol,Player=player,Shuffle=roll};
            Receive(message);
            foreach(var peer in Subscribers)if(peer.isAuthenticated&&peer.isReady)peer.Send(message);
        }
        internal static void RequestShuffle()
        {if(_installed&&_client!=null&&NetworkClient.ready)_client.Send(new DongShuffleRequest{Protocol=Protocol});}
        internal static void Tick()
        {
            if(!_installed)return;
            if(NetworkServer.active&&!_wasServer)
            {
                ServerRolls.Clear();LastPress.Clear();Subscribers.Clear();
                NetworkServer.RegisterHandler<DongAppearanceRequest>(Snapshot,requireAuthentication:true);
                NetworkServer.RegisterHandler<DongShuffleRequest>(Shuffle,requireAuthentication:true);
            }
            if(!NetworkServer.active&&_wasServer){ServerRolls.Clear();LastPress.Clear();Subscribers.Clear();}
            _wasServer=NetworkServer.active;
            Subscribers.RemoveWhere(c=>{bool gone=!NetworkServer.connections.TryGetValue(c.connectionId,out var live)||!ReferenceEquals(live,c);if(gone)LastPress.Remove(c);return gone;});
            if(_client!=NetworkClient.connection||!NetworkClient.active){_client=null;_wasReady=false;ClientRolls.Clear();}
            if(!NetworkClient.ready)_wasReady=false;
            if(NetworkClient.active&&NetworkClient.isConnected&&NetworkClient.ready&&(_client==null||!_wasReady))
            {RegisterReceiver();_client=NetworkClient.connection;_wasReady=true;_client.Send(new DongAppearanceRequest{Protocol=Protocol});}
            if(Keyboard.current!=null&&Keyboard.current.f7Key.wasPressedThisFrame)RequestShuffle();
            DongShuffleButton.Tick();
        }
        internal static void Shutdown()
        {
            _installed=_wasServer=_wasReady=false;_client=null;ServerRolls.Clear();ClientRolls.Clear();LastPress.Clear();Subscribers.Clear();
            NetworkClient.UnregisterHandler<DongAppearanceMessage>();NetworkServer.UnregisterHandler<DongAppearanceRequest>();NetworkServer.UnregisterHandler<DongShuffleRequest>();
            DongShuffleButton.Shutdown();
        }
    }
}
