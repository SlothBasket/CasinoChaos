using System;
using System.Collections.Generic;
using Dissonance.Integrations.FMOD_Playback;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal static class BodyPartEffects
    {
        private sealed class World
        { internal EventInstance Instance; internal IntPtr Group; internal FmodDspChain Chain; internal float Strength; }
        private sealed class Voice
        { internal FMODVoicePlayback Playback; internal IntPtr Channel; internal FmodDspChain Chain; internal float Strength; internal bool Mouth; }
        private static readonly Dictionary<IntPtr, World> Worlds = new Dictionary<IntPtr, World>();
        private static readonly Dictionary<int, Voice> Voices = new Dictionary<int, Voice>();
        private static readonly Dictionary<AudioSource, UnityHearingModifier> UnitySources = new Dictionary<AudioSource, UnityHearingModifier>();
        private static readonly List<IntPtr> DeadWorlds = new List<IntPtr>();
        private static readonly List<int> DeadVoices = new List<int>();
        private static readonly List<AudioSource> DeadSources = new List<AudioSource>();
        private static readonly AccessTools.FieldRef<FMODVoicePlayback, Channel> VoiceChannel = AccessTools.FieldRefAccess<FMODVoicePlayback, Channel>("_channel");
        private static readonly AccessTools.FieldRef<VoipManipulationManager, Dictionary<string, bool>> MouthStates = AccessTools.FieldRefAccess<VoipManipulationManager, Dictionary<string, bool>>("_mouthFX");
        private static readonly System.Reflection.MethodInfo MouthParam = AccessTools.Method(typeof(VoipManipulation), "SetMouthFXParam");
        private static StudioListener _listener;
        private static bool _installed, _seeded, _earsActive, _refresh;
        private static float _nextTick, _nextSources;
        internal static bool Installed => _installed;
        internal static void Install()
        {
            _installed = true; _refresh = true;
            BodyPartState.Changed += Changed;
        }
        private static void Changed(ulong id, CustomBodyPart part, bool present) { if (part == CustomBodyPart.LeftEar || part == CustomBodyPart.RightEar) { _refresh = true; _nextTick = 0; } }
        internal static void Register(EventInstance instance)
        {
            if (!_installed || !NetworkClient.active || !instance.isValid() || Worlds.ContainsKey(instance.handle)) return;
            if (instance.getDescription(out var description) != RESULT.OK || description.is3D(out bool spatial) != RESULT.OK || !spatial) return;
            if (description.getPath(out string path) != RESULT.OK || !HearingRules.IsWorldEvent(path)) return;
            Worlds.Add(instance.handle, new World { Instance = instance });
        }
        internal static void Register(FMODVoicePlayback playback)
        {
            if (_installed && playback && !Voices.ContainsKey(playback.GetInstanceID()))
                Voices.Add(playback.GetInstanceID(), new Voice { Playback = playback });
        }
        internal static void Unregister(FMODVoicePlayback playback)
        {
            if (!playback || !Voices.TryGetValue(playback.GetInstanceID(), out var voice)) return;
            voice.Chain?.Dispose(); Voices.Remove(playback.GetInstanceID());
        }
        private static bool MissingMouth(FMODVoicePlayback playback)
        {
            var manager = playback.GetComponentInParent<VoipManipulationManager>();
            return manager && playback.PlayerName != null && MouthStates(manager).TryGetValue(playback.PlayerName, out bool missing) && missing;
        }
        private static float Strength(Vector3 position, bool left, bool right, float current, float smoothing)
        {
            if (left && right) return 0;
            float side = Vector3.Dot(_listener.transform.right, (position - _listener.transform.position).normalized);
            return Mathf.Lerp(current, HearingRules.Strength(left, right, side), smoothing);
        }
        private static Vector3 Position(VECTOR value) => new Vector3(value.x, value.y, value.z);
        internal static void Tick()
        {
            if (!_installed || Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + HearingTuning.AudioTickSeconds;
            if (!RuntimeManager.IsInitialized) return;
            if (!_seeded && NetworkClient.active)
            {
                _seeded = true;
                if (RuntimeManager.StudioSystem.getBankList(out var banks) == RESULT.OK)
                    foreach (var bank in banks)
                        if (bank.getEventList(out var events) == RESULT.OK)
                            foreach (var description in events)
                                if (description.getInstanceList(out var instances) == RESULT.OK)
                                    foreach (var instance in instances) Register(instance);
                foreach (var playback in UnityEngine.Object.FindObjectsByType<FMODVoicePlayback>(FindObjectsSortMode.None)) Register(playback);
                // Remove authored mouth EQ for hot-loaded existing playback too.
                foreach (var manipulation in UnityEngine.Object.FindObjectsByType<VoipManipulation>(FindObjectsSortMode.None)) MouthParam.Invoke(manipulation, new object[] { 0 });
            }
            if (!NetworkClient.active) _seeded = false;
            var camera = Camera.main;
            if (camera && camera.TryGetComponent<StudioListener>(out var cameraListener)) _listener = cameraListener;
            if (!_listener || !_listener.isActiveAndEnabled) _listener = UnityEngine.Object.FindAnyObjectByType<StudioListener>();
            var profile = NetworkClient.localPlayer ? NetworkClient.localPlayer.GetComponent<PlayerProfile>() : null;
            var body = profile ? BodyPartState.Get(profile.steamId) : BodyState.Complete;
            bool left = body.Has(CustomBodyPart.LeftEar), right = body.Has(CustomBodyPart.RightEar);
            bool ears = _listener && (!left || !right);
            if (ears != _earsActive) { _earsActive = ears; CasinoChaosPlugin.Log("Ear audio controller " + (ears ? "enabled" : "disabled")); }
            float smoothing = _refresh ? 1 : 1 - Mathf.Exp(-HearingTuning.AudioTickSeconds / HearingTuning.DirectionSmoothingSeconds);
            _refresh = false;
            DeadWorlds.Clear();
            foreach (var entry in Worlds)
            {
                var world = entry.Value;
                if (!world.Instance.isValid() || world.Instance.getPlaybackState(out var state) != RESULT.OK || state == PLAYBACK_STATE.STOPPED)
                { world.Chain?.Dispose(); DeadWorlds.Add(entry.Key); continue; }
                if (!ears) { world.Chain?.Dispose(); world.Chain = null; world.Group = IntPtr.Zero; world.Strength = 0; continue; }
                if (world.Instance.get3DAttributes(out var attributes) != RESULT.OK || world.Instance.getChannelGroup(out var group) != RESULT.OK) continue;
                if (world.Group != group.handle) { world.Chain?.Dispose(); world.Chain = null; world.Group = group.handle; }
                world.Strength = Strength(Position(attributes.position), left, right, world.Strength, smoothing);
                HearingRules.Parameters(!left && !right, world.Strength, out float gain, out float cutoff);
                try { if (world.Chain == null) world.Chain = new FmodDspChain(group); world.Chain.Set(gain, cutoff, 0); }
                catch (Exception error) { world.Chain?.Dispose(); DeadWorlds.Add(entry.Key); CasinoChaosPlugin.Log("World audio layer unavailable: " + error.Message); }
            }
            foreach (var key in DeadWorlds) Worlds.Remove(key);
            DeadVoices.Clear();
            foreach (var entry in Voices)
            {
                var voice = entry.Value; var playback = voice.Playback;
                if (!playback || !playback.isActiveAndEnabled) { voice.Chain?.Dispose(); DeadVoices.Add(entry.Key); continue; }
                var channel = VoiceChannel(playback);
                bool mouth = MissingMouth(playback);
                if (mouth != voice.Mouth) { voice.Mouth = mouth; CasinoChaosPlugin.Log($"Mouth voice effect {(mouth ? "applied" : "removed")}: speaker='{playback.PlayerName}'"); }
                if (voice.Channel != channel.handle) { voice.Chain?.Dispose(); voice.Chain = null; voice.Channel = channel.handle; voice.Strength = 0; }
                bool spatial = channel.getMode(out var mode) == RESULT.OK && (mode & MODE._3D) != 0;
                if (ears && spatial && channel.get3DAttributes(out var pos, out _) == RESULT.OK)
                    voice.Strength = Strength(Position(pos), left, right, voice.Strength, smoothing);
                else voice.Strength = 0;
                HearingRules.Parameters(!left && !right, voice.Strength, out float gain, out float cutoff);
                if (mouth) { gain *= HearingTuning.MouthGain; cutoff = Mathf.Min(cutoff, HearingTuning.MouthCutoffHz); }
                if (!mouth && voice.Strength == 0) { voice.Chain?.Dispose(); voice.Chain = null; continue; }
                if (!channel.hasHandle()) continue;
                try { if (voice.Chain == null) voice.Chain = new FmodDspChain(channel); voice.Chain.Set(gain, cutoff, mouth ? HearingTuning.MouthDistortion : 0); }
                catch (Exception error) { voice.Chain?.Dispose(); voice.Chain = null; DeadVoices.Add(entry.Key); CasinoChaosPlugin.Log("Voice audio layer unavailable: " + error.Message); }
            }
            foreach (var key in DeadVoices) Voices.Remove(key);
            TickUnity(ears, left, right, smoothing);
        }
        private static bool Eligible(AudioSource source) => source && source.isActiveAndEnabled && source.spatialBlend > 0.01f
            && !source.name.StartsWith("UI", StringComparison.OrdinalIgnoreCase)
            && !source.name.StartsWith("Menu", StringComparison.OrdinalIgnoreCase)
            && !source.name.StartsWith("System", StringComparison.OrdinalIgnoreCase)
            && (source.name + (source.clip ? source.clip.name : "") + (source.outputAudioMixerGroup ? source.outputAudioMixerGroup.name : "")).IndexOf("music", StringComparison.OrdinalIgnoreCase) < 0;
        internal static void RegisterUnitySource(AudioSource source)
        {
            if(_installed&&_earsActive&&Eligible(source)&&!UnitySources.ContainsKey(source)&&source.GetComponents<AudioSource>().Length==1)
                UnitySources.Add(source,source.gameObject.AddComponent<UnityHearingModifier>());
        }
        private static void TickUnity(bool ears, bool left, bool right, float smoothing)
        {
            if (ears && Time.unscaledTime >= _nextSources)
            {
                _nextSources = Time.unscaledTime + HearingTuning.UnitySourceRefreshSeconds;
                foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    if (Eligible(source) && !UnitySources.ContainsKey(source) && source.GetComponents<AudioSource>().Length == 1)
                    { UnitySources.Add(source, source.gameObject.AddComponent<UnityHearingModifier>()); CasinoChaosPlugin.Log("Registered spatial Unity AudioSource: " + source.name); }
            }
            DeadSources.Clear();
            foreach (var entry in UnitySources)
            {
                if (!ears || !Eligible(entry.Key) || !entry.Value)
                { if (entry.Value) { entry.Value.Set(1, HearingTuning.NormalCutoffHz); UnityEngine.Object.Destroy(entry.Value); } DeadSources.Add(entry.Key); continue; }
                var modifier = entry.Value;
                modifier.Strength = Strength(entry.Key.transform.position, left, right, modifier.Strength, smoothing);
                HearingRules.Parameters(!left && !right, modifier.Strength, out float gain, out float cutoff); modifier.Set(gain, cutoff);
            }
            foreach (var key in DeadSources) UnitySources.Remove(key);
        }
        internal static void Shutdown()
        {
            _installed = false; BodyPartState.Changed -= Changed;
            foreach (var world in Worlds.Values) world.Chain?.Dispose(); Worlds.Clear();
            foreach (var voice in Voices.Values) voice.Chain?.Dispose(); Voices.Clear();
            foreach (var modifier in UnitySources.Values) if (modifier) { modifier.Set(1, HearingTuning.NormalCutoffHz); UnityEngine.Object.Destroy(modifier); }
            UnitySources.Clear();
            // Called AFTER unpatching: restore the game's own receiver-side EQ.
            if (RuntimeManager.IsInitialized)
                foreach (var manipulation in UnityEngine.Object.FindObjectsByType<VoipManipulation>(FindObjectsSortMode.None))
                {
                    var playback = manipulation.GetComponent<FMODVoicePlayback>();
                    if (playback) MouthParam.Invoke(manipulation, new object[] { MissingMouth(playback) ? 1 : 0 });
                }
            _seeded = false; _listener = null; _earsActive = false; _nextTick = _nextSources = 0;
        }
    }
    [HarmonyPatch(typeof(EventInstance), nameof(EventInstance.start))]
    internal static class WorldAudioRegistration
    { private static void Postfix(EventInstance __instance, RESULT __result) { if (__result == RESULT.OK) BodyPartEffects.Register(__instance); } }
    [HarmonyPatch(typeof(FMODVoicePlayback), "OnEnable")]
    internal static class VoiceAudioRegistration
    { private static void Postfix(FMODVoicePlayback __instance) => BodyPartEffects.Register(__instance); }
    [HarmonyPatch(typeof(FMODVoicePlayback), "OnDisable")]
    internal static class VoiceAudioDisable
    { private static void Prefix(FMODVoicePlayback __instance) => BodyPartEffects.Unregister(__instance); }
    [HarmonyPatch(typeof(FMODVoicePlayback), "OnDestroy")]
    internal static class VoiceAudioDestroy
    { private static void Prefix(FMODVoicePlayback __instance) => BodyPartEffects.Unregister(__instance); }
    [HarmonyPatch(typeof(VoipManipulation), "SetMouthFXParam")]
    internal static class ReplaceVanillaMouthFilter
    { private static void Prefix(ref int i) { if (BodyPartEffects.Installed) i = 0; } }
}
