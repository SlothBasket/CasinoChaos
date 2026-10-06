# CasinoChaos

Mod source for Gamble With Your Friends using BepInEx, Harmony, Mirror and GWYF Mod API.

This commit preserves the ranged-guard implementation immediately before the audio/body-part pass. The following feature commit will contain the current source and an exact diff for review.

## Build

Install the game, BepInEx and GWYF Mod API locally. This repository does not contain game DLLs, decompiled source or third-party binaries. The project targets .NET Standard 2.1; references are resolved from the local installation by `CasinoChaos/Directory.Build.props`.

```powershell
dotnet build CasinoChaos/GWYF_CasinoChaos.csproj -c Debug
```

The existing build target deploys the compiled mod DLL to the installed game's `BepInEx/gwyf_mods` folder. Override `GameDir` or `ModsDir` at build time for another installation/output location.
