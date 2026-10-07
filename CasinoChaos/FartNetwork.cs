using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

namespace GWYF_CasinoChaos
{
    internal struct FartSubscribe : NetworkMessage { internal byte Protocol; }
    internal struct FartRequest : NetworkMessage { internal byte Protocol; }
    internal struct FartMessage : NetworkMessage { internal byte Protocol; internal uint Player; internal byte Clip; internal byte Pitch; }
    internal static class FartNetwork
    {
        private const byte Protocol=3;
        private static readonly System.Random ClipRandom=new System.Random();
        private static bool _installed,_wasServer,_wasReady;
        private static NetworkConnectionToServer _client;
        private static readonly HashSet<NetworkConnectionToClient> Subscribers=new HashSet<NetworkConnectionToClient>();
        private static readonly Dictionary<NetworkConnectionToClient,float> Last=new Dictionary<NetworkConnectionToClient,float>();
        internal static void Install()
        {
            if(_installed)return;_installed=true;
            Writer<FartSubscribe>.write=(w,v)=>w.WriteByte(v.Protocol);
            Reader<FartSubscribe>.read=r=>new FartSubscribe{Protocol=r.ReadByte()};
            Writer<FartRequest>.write=(w,v)=>w.WriteByte(v.Protocol);
            Reader<FartRequest>.read=r=>new FartRequest{Protocol=r.ReadByte()};
            Writer<FartMessage>.write=(w,v)=>{w.WriteByte(v.Protocol);w.WriteUInt(v.Player);w.WriteByte(v.Clip);w.WriteByte(v.Pitch);};
            Reader<FartMessage>.read=r=>new FartMessage{Protocol=r.ReadByte(),Player=r.ReadUInt(),Clip=r.ReadByte(),Pitch=r.ReadByte()};
            RegisterReceiver();
        }
        internal static void RegisterReceiver(){if(_installed)NetworkClient.RegisterHandler<FartMessage>(Receive,requireAuthentication:true);}
        private static void Receive(FartMessage message){if(message.Protocol==Protocol)FartAudio.Play(message.Player,message.Clip,message.Pitch);}
        private static void Subscribe(NetworkConnectionToClient conn,FartSubscribe message)
        {if(message.Protocol==Protocol&&conn.isReady)Subscribers.Add(conn);}
        private static void Press(NetworkConnectionToClient conn,FartRequest message)
        {
            if(message.Protocol!=Protocol||!conn.isReady||!Subscribers.Contains(conn)||!conn.identity)return;
            var profile=conn.identity.GetComponent<PlayerProfile>();
            if(!profile||!BodyPartNetwork.TryGetServerState(profile.steamId,out var state)||!FartRules.CanAttempt(state))return;
            if(Last.TryGetValue(conn,out var previous)&&Time.unscaledTime-previous<1f)return;
            Last[conn]=Time.unscaledTime;
            byte clip=FartRules.ChooseClip(state,ClipRandom);
            var sound=new FartMessage{Protocol=Protocol,Player=conn.identity.netId,Clip=clip,Pitch=FartRules.ChoosePitch(clip,ClipRandom)};
            // Send once, including the host's local connection. No direct local
            // playback here, which would double-play on a listen server.
            foreach(var peer in Subscribers)if(peer.isAuthenticated&&peer.isReady)peer.Send(sound);
        }
        internal static void Request(){if(_installed&&_client!=null&&NetworkClient.ready)_client.Send(new FartRequest{Protocol=Protocol});}
        internal static void Tick()
        {
            if(!_installed)return;
            if(NetworkServer.active&&!_wasServer)
            {
                Subscribers.Clear();Last.Clear();
                NetworkServer.RegisterHandler<FartSubscribe>(Subscribe,requireAuthentication:true);
                NetworkServer.RegisterHandler<FartRequest>(Press,requireAuthentication:true);
            }
            if(!NetworkServer.active&&_wasServer){Subscribers.Clear();Last.Clear();}
            _wasServer=NetworkServer.active;
            Subscribers.RemoveWhere(c=>{bool dead=!NetworkServer.connections.TryGetValue(c.connectionId,out var live)||!ReferenceEquals(c,live);if(dead)Last.Remove(c);return dead;});
            if(_client!=NetworkClient.connection||!NetworkClient.active){_client=null;_wasReady=false;}
            if(!NetworkClient.ready)_wasReady=false;
            if(NetworkClient.active&&NetworkClient.isConnected&&NetworkClient.ready&&(_client==null||!_wasReady))
            {RegisterReceiver();_client=NetworkClient.connection;_wasReady=true;_client.Send(new FartSubscribe{Protocol=Protocol});}
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            var typing=selected?selected.GetComponent<TMP_InputField>():null;
            if(!(typing&&typing.isFocused)&&Keyboard.current!=null&&Keyboard.current.fKey.wasPressedThisFrame)
            {
                var profile=NetworkClient.localPlayer?NetworkClient.localPlayer.GetComponent<PlayerProfile>():null;
                if(profile&&FartRules.CanAttempt(BodyPartState.Get(profile.steamId)))Request();
            }
            FartButton.Tick();FartAudio.Tick();
        }
        internal static void Shutdown()
        {
            _installed=_wasServer=_wasReady=false;_client=null;Subscribers.Clear();Last.Clear();
            NetworkClient.UnregisterHandler<FartMessage>();NetworkServer.UnregisterHandler<FartSubscribe>();NetworkServer.UnregisterHandler<FartRequest>();
            FartButton.Shutdown();FartAudio.Shutdown();
        }
    }
}
