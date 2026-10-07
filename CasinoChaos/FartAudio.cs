using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FMOD;
using FMODUnity;
using Mirror;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal static class FartAudio
    {
        private sealed class Voice
        { internal Transform Body; internal Channel Channel; internal FmodDspChain Chain; }
        private static readonly Sound[] Clips = new Sound[FartClipData.Names.Length];
        private static readonly List<Voice> Voices=new List<Voice>();
        private static void Check(RESULT result)
        { if(result!=RESULT.OK)throw new InvalidOperationException("Fart FMOD: "+result); }
        private static Sound Clip(int index)
        {
            if(Clips[index].hasHandle())return Clips[index];
            using(var stream=FartClipData.Open(index))
            using(var memory=new MemoryStream())
            {
                stream.CopyTo(memory);var bytes=memory.ToArray();
                var info=new CREATESOUNDEXINFO{cbsize=Marshal.SizeOf<CREATESOUNDEXINFO>(),length=(uint)bytes.Length};
                // OPENMEMORY copies the WAV data; the managed buffer can then go away.
                Check(RuntimeManager.CoreSystem.createSound(bytes,MODE.OPENMEMORY|MODE.CREATESAMPLE|MODE._3D|MODE._3D_LINEARROLLOFF|MODE.LOOP_OFF,ref info,out var sound));
                Clips[index]=sound;return sound;
            }
        }
        private static Vector3 Position(Transform body)=>body.TransformPoint(new Vector3(0,-.10f,-.32f));
        private static void Pose(Voice voice)
        {
            var p=Position(voice.Body);var position=new VECTOR{x=p.x,y=p.y,z=p.z};var velocity=new VECTOR();
            Check(voice.Channel.set3DAttributes(ref position,ref velocity));
            var listener=Camera.main?Camera.main.GetComponent<StudioListener>():null;
            if(!listener||!listener.isActiveAndEnabled)listener=UnityEngine.Object.FindAnyObjectByType<StudioListener>();
            var profile=NetworkClient.localPlayer?NetworkClient.localPlayer.GetComponent<PlayerProfile>():null;
            var state=profile?BodyPartState.Get(profile.steamId):BodyState.Complete;
            bool left=state.Has(CustomBodyPart.LeftEar),right=state.Has(CustomBodyPart.RightEar);
            float strength=listener?HearingRules.Strength(left,right,Vector3.Dot(listener.transform.right,(p-listener.transform.position).normalized)):0;
            HearingRules.Parameters(!left&&!right,strength,out float gain,out float cutoff);
            voice.Chain.Set(gain,cutoff,0);
        }
        internal static void Play(uint player,byte index,byte pitch)
        {
            if(index>=Clips.Length||pitch<92||pitch>108||!NetworkClient.spawned.TryGetValue(player,out var identity)||!identity)return;
            if(!RuntimeManager.IsInitialized){CasinoChaosPlugin.Log("Fart playback skipped: FMOD is not initialized.");return;}
            var body=identity.transform.Find(DongVisualTuning.BodyPath);if(!body)body=identity.transform;
            Voice voice=null;
            try
            {
                var clip=Clip(index);
                Check(RuntimeManager.CoreSystem.getMasterChannelGroup(out var group));
                Check(RuntimeManager.CoreSystem.playSound(clip,group,true,out var channel));
                voice=new Voice{Body=body,Channel=channel};
                Check(channel.setVolume(.8f));Check(channel.set3DMinMaxDistance(1,12));Check(channel.setPitch(pitch/100f));
                voice.Chain=new FmodDspChain(channel);Pose(voice);
                Check(channel.setPaused(false));Voices.Add(voice);
                CasinoChaosPlugin.Log($"Fart playing: player={player}; clip={FartClipData.Names[index]}; pitch={pitch/100f:F2}; FMOD positional audio.");
            }
            catch(Exception error){if(voice!=null)Stop(voice);CasinoChaosPlugin.Log(error.Message);}
        }
        private static void Stop(Voice voice){voice.Chain?.Dispose();voice.Channel.stop();}
        internal static void Tick()
        {
            for(int i=Voices.Count-1;i>=0;i--)
            {
                var voice=Voices[i];
                if(!voice.Body||voice.Channel.isPlaying(out bool playing)!=RESULT.OK||!playing){Stop(voice);Voices.RemoveAt(i);continue;}
                try{Pose(voice);}catch(Exception error){Stop(voice);Voices.RemoveAt(i);CasinoChaosPlugin.Log(error.Message);}
            }
        }
        internal static void Shutdown()
        {
            foreach(var voice in Voices)Stop(voice);Voices.Clear();
            for(int i=0;i<Clips.Length;i++){if(Clips[i].hasHandle())Clips[i].release();Clips[i].clearHandle();}
        }
    }
}
