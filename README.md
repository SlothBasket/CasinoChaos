# CasinoChaos

BepInEx/Harmony mod for **Gamble With Your Friends**, using GWYF Mod API and Mirror. Current source version: **1.6.3**. Every player must use the same compatible CasinoChaos DLL for the current custom body state and audio features.

All current mod code is in [CasinoChaos/](CasinoChaos/). See the [development notes](CasinoChaos/README.md) for feature history and implementation details. Earlier notes in docs describe older passes.

## Current features

- Civilian bat rewards, global heat escalation/HUD, mafia gun/bat guards and defeat behavior.
- Lobby machine buy/sell for Eye, Ear, Mouth, Dong, Leg, Body and Butt, with host-authoritative transactions and custom state synchronization.
- Missing-eye world blur/blindness and vanilla eye-model handling; directional underwater missing-ear hearing and mouthless speech processing.
- Missing-leg movement changes and red remnants; visible ears, cheeks, randomized dong appearance/swing, missing-part wounds and a development size shuffle.
- F fart control and pause-menu button: host selects among six embedded clips with Butt present; without Butt, the supplied dry puff plays with synchronized random pitch from .92 to 1.08. Positional FMOD playback respects missing-ear filtering and the one-second cooldown.

## Build

Install the game, BepInEx and GWYF Mod API. Game references resolve through [Directory.Build.props](CasinoChaos/Directory.Build.props). The normal build deploys only the compiled mod DLL to BepInEx/gwyf_mods:

```powershell
dotnet build CasinoChaos/GWYF_CasinoChaos.csproj -c Debug
```

To build without deploying into the game:

```powershell
dotnet build CasinoChaos/GWYF_CasinoChaos.csproj -c Debug -p:ModsDir="$PWD/build-output"
```

The seven user-supplied audio clips are packaged as WAV resources inside the DLL; clients do not need separate audio files.

## Controls

F or Escape → Fart attempts a fart. F7/pause-menu shuffle rerolls dong appearance. Host Debug controls: [ / ] toggle LeftEar/RightEar; F8 toggles Mouth; F10 selects a player; Ctrl+Shift+Home/End toggle legs; Ctrl+Shift+Insert adds global heat and Ctrl+Shift+Delete resets heat. F6 remains the Mod Manager key.

## Validation

The current Debug build passes with no warnings/errors. Test projects require .NET 10; native audio checks require the installed game's FMOD library and use NOSOUND output:

```powershell
dotnet run --project tests/BodyAudio/BodyAudio.Tests.csproj
dotnet run --project tests/FmodDsp/FmodDsp.Tests.csproj
dotnet run --project tests/DryPuff/DryPuff.Tests.csproj
```

The dry-puff test covers all 64 ownership masks, unknown-state rejection, seven embedded resources and native playback/pitch settings. Build the mod in Debug first. Automated checks do not verify audible output, visual placement or live multiplayer.

## Repository scope

Mod source, build metadata, documentation, tests and the supplied sound clips are included. Decompiled GameReference source, installed game assets/assemblies, audio banks and build outputs are excluded. Assembly-CSharp.dll is never modified.
