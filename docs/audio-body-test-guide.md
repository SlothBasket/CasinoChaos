# CasinoChaos 1.1.0 — audio/body-part pass

Implemented and deployed the Debug development DLL to the existing BepInEx/gwyf_mods destination. All lobby members must install the same compatible CasinoChaos build. Compiled binaries are not committed to this repository. Debug and Release use the same body/audio protocol; Release excludes the development keys.

## Actual audio architecture

1. LocalManager creates the Resources Camera prefab. Its FMODUnity.StudioListener is the production listener; this is not an AudioListener/AudioSource-only game. The effect uses that camera's right vector and position, with an active StudioListener fallback.
2. SFXManager.SFXOneShot, SFXOneShotWithParameters, SFXOneShot3DAttachedWithParameters and SFXOneShot3DAttached create/play FMOD event instances. One-shots are released after starting; attached instances update their positions. StudioEventEmitter supplies additional event/loop paths. Hooking FMOD.Studio.EventInstance.start covers these managed event starts together.
3. The installed Master bank contains 210 events marked 3D and 78 marked 2D. These are event definitions, not active sources. Some UI/music events are marked 3D, so spatial status alone is insufficient. The implementation also excludes UI/GenericUI/Menu/System event prefixes and paths containing music. Production scene audio is predominantly FMOD; Unity AudioSources exist in resources/demo content and receive a periodic fallback scan only while an ear is missing.
4. Vanilla mouth loss travels through PlayerOrgans.SetMouth -> PlayerVoiceFX.RpcSetNoMouthFX -> SetNoMouthFXRoutine -> VoipManipulationManager.SetPlayerNoMouthFX -> VoipManipulation.SetMouthFXParam -> the FMOD global parameter NO MOUTH <bus index>.
5. This effect happens on each receiving client's voice playback, not microphone capture/transmission. There is no separate vanilla mouth volume scalar: the tested voice bus/group volume stays at 1. The bank enables a THREE_EQ with low/high bands at -80 dB, mid at +1 dB, and crossovers at 90/380 Hz. That extremely narrow pass band accounts for the perceived quietness.
6. The active voice prefab is FMODPlaybackPrefabWithBlackjackAnd, with Dissonance.Integrations.FMOD_Playback.FMODVoicePlayback and VoipManipulation. Playback uses the private FMOD Channel _channel and a Dissonance generator DSP, not the unused Unity VoicePlayback/AudioSource prefabs. Dissonance pools playback objects and recreates/tears down the channel in OnEnable/OnDisable/OnDestroy. VoipManipulation routes each speaker into a numbered VX bus.
7. Owned FMOD FADER, LOWPASS_SIMPLE and DISTORTION DSPs provide a multiplicative layer on the world event's ChannelGroup or the remote speaker's Channel. Vanilla channel/bus volumes, mixer settings, mutes and Dissonance PlaybackVolume remain untouched. The Unity fallback processes samples with its own OnAudioFilterRead component and does not overwrite AudioSource.volume or existing filters.
8. FMOD low-pass filtering is the main muffling method. The Unity fallback uses a simple managed low-pass. CasinoChaos removes only its own DSPs/components. Unity objects with multiple AudioSources are skipped to avoid applying a shared filter to unintended co-located 2D audio.
9. Vanilla logical mouth state and organ RPCs remain intact. Only activation of the authored NO MOUTH EQ is replaced. The speaker's mouth gain multiplies listener ear gain; cutoff is the lower of the two values, with mouth-only mild distortion. Other vanilla VOIP effects remain active. Unloading removes owned DSPs and restores the authored vanilla mouth parameter from the current logical state.
10. Supplementary BodyPartState stores LeftEar/RightEar separately from OrganType/PlayerOrganData. The host owns states keyed by PlayerProfile.steamId, broadcasts versioned/revisioned Mirror messages, and supplies a snapshot when a client becomes ready. Clients receive a Changed event; audio observes it. There is no client handler granting/removing ears. State survives scene changes during a server session and resets with a new server session/disconnect. This project is not Mirror-weaved, so serializers and handlers are registered explicitly; no new player NetworkBehaviour is required.

## Initial tuning

- Missing-side strength: dot(listener.right, normalized source direction), mirrored for left/right; 0.08-second smoothing while moving/turning.
- Directly impaired side: gain 0.35, low-pass 2200 Hz. Front/back and good side are neutral.
- Both ears missing: gain 0.25, cutoff 1200 Hz throughout eligible spatial world audio and spatial voice. UI/music/non-spatial system audio remains excluded.
- Mouthless remote speaker: gain 0.55, cutoff 2200 Hz, distortion 0.08. This replaces the narrow vanilla mouth EQ rather than stacking more attenuation over it. The 0.55 scalar is an initial setting, not a measured guarantee of perceived loudness.
- Body-state changes refresh local hearing immediately on the next audio update (at most about 20 ms); moving source/listener changes interpolate. World instances are registered at start and existing loops are seeded once when the client session starts. Unity fallback discovery occurs every 0.5 seconds only while ears are missing.

## Short development test

1. Install the same Debug DLL on both players and restart the game. Host a lobby and join with the second player.
2. Host: F6 toggles the selected player's left ear; F7 toggles right ear. Selection defaults to the host. F10 cycles players and logs the selected name/Steam ID. F8 toggles that player's vanilla mouth through OrganManager.ServerToggleOrgan. These keys cannot authoritatively change parts from a remote client and are excluded from Release.
3. With one ear missing, stand by a spatial machine/environment sound and turn: impaired side should be quiet/muffled; front/back/good side should be normal. Have the other player speak and walk around the listener to check moving positional voice. F10 lets the host select the remote listener and repeat ear tests there.
4. Restore ears with the same keys, then remove both. Confirm normal restoration and strong bilateral world/voice muffling without silence. UI/music should remain normal.
5. Select one player and toggle their mouth with F8. The other listener should hear louder speech than vanilla mouth loss, with muffling and mild distortion. Combine a missing ear on the listener with a missing mouth on the speaker; then restore each separately. The speaker hears no new self-monitoring audio.
6. Change scenes, late-join/reconnect, and verify the host's ear state snapshot is reflected on the correct player. Existing vanilla mouth join behavior is retained.
7. Unload/reload through the usual mod lifecycle and check ordinary hearing returns, with the game's original mouth effect restored when a mouth is still missing. State-change logs should appear without directional per-frame spam. Existing F9 heat testing remains unchanged.

## Validation and limits

- Debug and Release builds passed with zero compiler warnings/errors. NuGet vulnerability lookup was unavailable during the initial build; subsequent checks disabled that network-only audit.
- 132 functional checks passed for directional behavior, tuning bounds, player-state separation, revision/duplicate rejection and restoration events.
- The installed FMOD native library passed a headless test of the actual owned ChannelGroup DSP chain: attachment, composed gain/low-pass/distortion values, neutral bypass, complete removal, and preservation of the original group volume.
- Built Debug and deployed Debug DLL hashes match: 1E4DC62C23F0A5F14AEF7E5568F853429B1BB965A1289B9F9DF4B78C1542CD6E.
- Assembly-CSharp.dll remains unchanged: F2147868930F1FC9DF3489EF89339759F7EEB8A92C270BEC094A4ECE6DD2935B. GameReference was not edited. Only the normal development mod DLL was written into the game installation.
- No live Unity session or two-player audio test was performed. Harmony startup, live Mirror synchronization, voice pool reuse, audible tuning and hot reload still require the above in-game tests. Very brief effects may finish before the 20 ms audio update attaches a DSP. Event-path/source-name classification is deliberately conservative and may need adjustment for a specific map sound. Direct raw Core FMOD channels outside the inspected world-event path are not automatically captured.
