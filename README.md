# CasinoChaos

CasinoChaos is a BepInEx/Harmony mod for **Gamble With Your Friends**, built with GWYF Mod API and Mirror. Current source version: **1.1.0**.

**Every lobby member must install the same compatible CasinoChaos build for this audio/body-part pass.** Host authority remains responsible for gameplay and supplementary body state. This pass does not add body-part items or gambling.

## Start here for code review

The commit named **Add synchronized ears and receiver-side hearing/mouth effects** contains the actual audio/body-part changes relative to the imported ranged-guard baseline. All production mod code lives in [CasinoChaos/](CasinoChaos/).

- [BodyPartState.cs](CasinoChaos/BodyPartState.cs): supplementary LeftEar/RightEar state, revisions and change events.
- [BodyPartNetwork.cs](CasinoChaos/BodyPartNetwork.cs): host authority, Steam-ID keys, Mirror serialization, broadcasts, late-join snapshots and development controls.
- [BodyPartEffects.cs](CasinoChaos/BodyPartEffects.cs): local directional hearing, world/voice registration, Harmony hooks and restoration.
- [FmodDspChain.cs](CasinoChaos/FmodDspChain.cs): owned gain, low-pass and distortion DSPs without changing vanilla channel/bus volumes.
- [HearingRules.cs](CasinoChaos/HearingRules.cs): directional calculation, source classification and named tuning constants.
- [UnityHearingModifier.cs](CasinoChaos/UnityHearingModifier.cs): sample-processing fallback for eligible spatial Unity AudioSources.
- [CasinoChaosPlugin.cs](CasinoChaos/CasinoChaosPlugin.cs): plugin lifecycle and existing civilian reward patch.
- [Audio architecture and multiplayer test guide](docs/audio-body-test-guide.md).

Earlier civilian reward, per-player heat, mafia gun/bat and defeat behavior remain in the source. [Historical guard notes](docs/prior-guard-development-notes.md) describe those passes; their old vanilla-client assumption is superseded for the current audio pass. The audio pass does not add remote mafia gun presentation.

## Build

Install the game, BepInEx and GWYF Mod API locally. Use a .NET SDK capable of targeting .NET Standard 2.1. References resolve from the local installation through [Directory.Build.props](CasinoChaos/Directory.Build.props).

```powershell
dotnet build CasinoChaos/GWYF_CasinoChaos.csproj -c Debug
```

The existing target copies only the compiled development mod DLL into the game's `BepInEx/gwyf_mods` directory. For a different game location or an output folder that does not deploy into the game:

```powershell
dotnet build CasinoChaos/GWYF_CasinoChaos.csproj -c Debug -p:GameDir="D:\\SteamLibrary\\steamapps\\common\\Gamble With Your Friends" -p:ModsDir="D:\\CasinoChaosBuild"
```

## Development controls

On the host in a Debug build, **F6/F7** toggle the selected player's left/right ear, **F8** toggles their vanilla mouth, **F10** cycles the selected player (default host), and **F9** retains the existing heat shortcut. Remote clients cannot authoritatively toggle ears. Release excludes these controls.

## Validation

Debug and Release compiled with zero compiler warnings/errors. 132 functional checks passed for directional hearing, tuning, state separation, revision/duplicate rejection and restoration. The installed FMOD native library passed a headless test of DSP attachment, composed parameters, bypass/removal and unchanged underlying group volume.

The test projects require .NET 10:

```powershell
dotnet run --project tests/BodyAudio/BodyAudio.Tests.csproj
dotnet run --project tests/FmodDsp/FmodDsp.Tests.csproj
```

The FMOD test also requires the installed game's FMODUnity/CoreModule assemblies and native FMOD library. It uses NOSOUND output and does not launch or modify the game.

**Live Unity startup, multiplayer synchronization, perceived voice tuning and hot reload still require in-game testing.** Automated/headless checks do not replace that test.

## Repository scope

Only mod-owned source, build metadata, tests and documentation are included. No decompiled GameReference source, Assembly-CSharp.dll, game assets, audio banks or third-party binaries are committed. The mod references the locally installed game; it does not modify the game DLL.
