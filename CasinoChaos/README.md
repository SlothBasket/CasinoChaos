# CasinoChaos 1.5.1 — local Dong visual, global heat and compact body machine

Build `GWYF_CasinoChaos.csproj` with the .NET CLI. The existing build target deploys only the compiled development DLL to the game's `BepInEx/gwyf_mods` folder. Game assemblies and GameReference remain reference-only.

## Shared heat

Each accepted civilian baseball-bat hit still awards the existing $5 cash reward and now adds one **global** heat point. The existing 0.15-second bat/NPC duplicate window remains. Guard hits cannot award civilian money/heat. Heat is server-owned; client messages cannot add or reset it. Attribution retains the offender controller/profile, Steam ID/name, per-player hit count, and most recent offender until reset.

| Level | Points | Desired surviving gun guards |
|---|---|---|
| 0 | 0–2 | 0 |
| 1 | 3–5 | 1 |
| 2 | 6–9 | 2 |
| 3 | 10–14 | 3 |
| 4 | 15–20 | 5 |
| 5 | 21+ | 7 |

On a tier **increase**, spawn only the shortfall from surviving active guards. New guards pursue the latest offender; existing assignments stay intact. Defeats, disconnects, failed spawns, and ordinary same-tier hits do not schedule replacement loops. Failed spawns are reconsidered only on the next tier transition. Corpses retain the existing 60-second lifetime, but round reset also removes them.

Default balancing is centralized in `HeatRules.Tiers`. Host configuration in the existing `com.gwyf.casinochaos.cfg`, section `Heat`, provides `Level1MinimumPoints` through `Level5MinimumPoints` and `Level1GunGuards` through `Level5GunGuards`. Changes apply at the next scene/day reset. Thresholds must increase strictly; invalid ordering retains the last valid table.

Reset hooks: server `GameManager.InitializeScene(string)` (actual scene/day initialization), `GameManager.ServerRetrySameDay()` (retry), leaving `GameState.Game`, existing active-scene change cleanup, server stop and mod unload. `StartDay()` only starts the timer after elevator travel and is intentionally not patched. Floors/elevator travel therefore do not reset heat. Lobby/intermission starts at zero.

## HUD and networking

A compact `HEAT 3 / 5` panel reuses `GameUI/TimerUI`'s Image style and its Day TMP object: the installed DangrekOutline font and Dangrek-Regular Atlas Material. It inherits the existing 1920×1080 CanvasScaler, sits 16 units left of the right-hand day/money/quota labels, aligned with their top edge, and uses a 144×36 rectangle. Tier increases pulse once by at most 4% for 0.35 seconds, then remain static. No flashing or shaking; join snapshots do not replay past pulses.

`HeatNetwork` follows the project's manual Mirror reader/writer architecture. An authenticated, ready client explicitly subscribes with `HeatSnapshotRequest`; the server replies with `HeatStateMessage` and sends subsequent point/reset changes only to subscribers. No per-frame heat messages. New connections and each post-scene readiness transition request a snapshot. No client-to-server heat value setter exists.

Heat/guards remain host-side and use vanilla NPC/network gameplay. **The custom heat HUD requires CasinoChaos on that client.** Vanilla clients receive no custom heat messages. Existing custom ears/body-machine features still have their established client-mod requirements. Existing host-local gun presentation limitations remain.

## Body-machine layout

`BodyMachineLayout` supplies one two-column layout, 55% of original button scale: `(0.22, 0.22, 0.165)`. Column centers X=-2.10/-1.70, rows Y=3.55/3.21/2.87/2.53/2.19, Z=2.50 in Model/Base coordinates. Ten entries fit before a geometry/layout review is required. Current order is EYE/EAR, MOUTH/DONG, LEG/BODY. Existing model, material, collider, feedback and interaction logic are reused. Originals restore their positions/scales on unload.

The existing PriceTag rectangle (0.8×1.5, on the opposite side from the buttons/lever) is split into two 0.38-wide TMP columns with a 0.04 gap. Entries/prices come from the same BodyPartDefinition order as the buttons, including LEG. No new font or custom art. Buying/selling authority and body state are unchanged.

## Development controls (Debug builds only, host)

- Ctrl+Shift+Insert: add one global heat point attributed to the local host, during a casino game; no cash change.
- Ctrl+Shift+Delete: reset global heat and despawn CasinoChaos guards.
- Existing controls: [ LeftEar, ] RightEar, F8 Mouth, F10 selects the body debug target, Ctrl+Shift+Home LeftLeg, Ctrl+Shift+End RightLeg.

F9's old per-player heat shortcut is removed. F6 remains reserved for the Mod Manager. Release builds omit these development controls.

## Smoke test

1. Host a casino floor; land three separate civilian bat hits. Expect $5 each, global level 1 and one gun guard.
2. Add crimes from a second player to six total; expect level 2, only one added guard, targeting the latest offender. Both modded clients should show the same level.
3. Defeat guards and stay in the same tier: no replacements. Raise from level 3 with one survivor to level 4: four reinforcements should bring the active count to five.
4. After 21 points, level stays at five. Defeat all guards; further hits in that capped tier must not replace them.
5. Return home/retry/start another day; heat returns to zero and all previous guards/corpses disappear. Elevator travel between floors must not reset heat.
6. Rejoin/load a scene with a modded client; confirm its HUD snapshot matches the host. A vanilla client should retain normal NPC movement/knockback without receiving custom heat messages.
7. Inspect both sides of the body machine: six compact buttons, readable paired price columns, all transactions/hover/press feedback working, lever unobstructed. Check another resolution and the existing vision accessibility modes.

Automated checks cover tier boundaries, reinforcement shortfalls/no replacement loops, server-only attribution/reset behavior using simulated guards, and ten-button collider spacing. Build/metadata checks do not replace live Unity layout and multiplayer tests.


## Dong visual and physics (1.5.3)

Every modded client creates a cosmetic hanging rectangle for each spawned player whose synchronized Dong state is present. State/economy/network protocol are unchanged. An unknown snapshot (revision zero) renders nothing until state arrives. There is no hinge-angle networking, projectile, collider, functional weapon, or extra NetworkBehaviour.

The inspected prefab has no named pelvis/hips or separate skeletal ragdoll. The attachment target is Player/Model/Body/BodyModel (SpringRotationFollowerY), with BodyMesh beneath it. Vanilla ragdoll and recovery rotate the same player root Rigidbody; the attachment follows the same hierarchy. Runtime setup resolves this exact path and logs it; it does not guess alternate bones. If the model is actually replaced, it removes the old rig and rebinds.

DongVisualTuning centralizes the BodyModel-local pivot (0, 0.03, 0.365), 0.04 mass, linear damping 0.35/angular damping 2, ±55 degree hinge limits and maximum angular speed 6 radians/sec. The pivot is lower and embedded in the body's front to close the side-view gap. DongAppearance supplies random dimensions: width 0.09–0.15, length 0.20–0.32, depth 0.08–0.13 metres. Each appearance roll chooses either local X (forward/back) or local Z (side-to-side), never both. The resting tip remains above the inspected body bottom. The runtime cube uses a separate copy of the existing body material/shader with a white texture; no game material is edited.

The per-player controller is on the player; its local cosmetic rig is a separate scene root. DongAnchor is kinematic and follows BodyModel using MovePosition/MoveRotation in FixedUpdate. DongVisual is a sibling dynamic Rigidbody connected only to this anchor by one HingeJoint, with its mesh centered below the pivot. A sibling, rather than a dynamic transform child of a moving anchor/player, lets physics provide the pendulum motion without inherited transform teleports. Explicit prism center-of-mass and inertia are required because no collider exists. The visual cannot push/snare the owner or become a bat/gun raycast target. Teleports rebase only this rig and clear only its own velocities. A late render update stitches the collider-free mesh top to the current BodyModel pivot while retaining the physics swing angle, avoiding a detached appearance between fixed physics and interpolated player frames. It does not move a Rigidbody or joint.

SelfMeshDisabler places the local body's mesh on SelfMeshPlayer. A local copy of the existing body-only mesh provides first-person attachment context when the camera culls that mesh. It shares the body material/property block, has no collider and excludes the separate head/face. It is hidden when Cameraman mode shows the original body. No camera masks or original body layers are changed; Cameraman's Hide Character renderer flag is respected. No third-person camera was added.

Missing Dong immediately deactivates and destroys the rig/joint/material. Restoration creates one rig. Discovery/pruning covers new spawn identities, profile/state arrival and reconnects. Scene cleanup and mod unload dispose all controllers and rigs; reloading does not reuse disposed objects. No collision-ignore bookkeeping exists to leak. Dedicated servers create no cosmetic rigs.

### Solo inspection

Host normally. Look straight down with Dong present. For an outside view, use Developer Console → Cameraman → Enable Cameraman Mode, keep character visibility enabled, close the console and move/look back toward your stationary body. Disable Cameraman Mode to return to gameplay. Use the existing DONG machine entry to sell/restore and confirm disappearance/reappearance; press F7 or use Escape → Shuffle Dong Size to re-roll your own size. This is available in Debug and Release; it re-rolls both size and swing axis.

### Live verification still required

Compare standing, walking, sprinting, jumping, mafia knockback, tumbling and recovery. Check attachment and bounded swing, unchanged movement/knockback, first-person visibility and outside-view placement. Sell/restore repeatedly, change scenes, respawn/reconnect and Ctrl+R reload; confirm one visible model per present state and none when missing/unloaded. With two modded clients, confirm ownership agrees while local swing angles may differ. The automated build and source/metadata checks do not execute Unity physics or verify live rendering.


### Appearance synchronization (1.5.2)

All modded clients derive the same initial size/axis from player identity. F7 and the new pause-menu button send an authenticated request for that connection's own player. The host verifies Dong ownership and increments a cosmetic shuffle counter once; a 0.3-second press guard rejects rapid duplicates. Clients receive that counter and reconstruct identical dimensions and axis. Ready/join snapshots include prior shuffles. No client-provided target, size or body-state changes are accepted; physics angles remain local. These cosmetic messages require updated CasinoChaos clients.


### Attachment and silhouette follow-up (1.5.3)

The root moved outward by 0.065 m from 1.5.2, leaving the rear edge embedded while allowing more of the hanging shape to clear the body. A 6-degree render-only outward tilt preserves the existing one-axis gravity hinge. A simple eight-sided mesh replaces the rectangular cube, with a subtly wider upper section and rounded distal end, using the existing solid body material. It has no collider; inertia uses its conservative bounding prism. Generated meshes are destroyed with the rig.

The local first-person render pose moves back 0.265 m and down 0.04 m relative to the world attachment; the body-only context copy receives the same adjustment. This only affects cosmetic rendering, not simulated physics, cameras, or other players. Cameraman mode and remote players use the world attachment. The existing F7/pause-menu request and authority path remain intact; the appearance generator now also changes axis on shuffle. Live first-/third-person placement verification is still required.


Size distribution now has 60% normal, 20% tiny and 20% oversized rolls. Normal width/length/depth ranges remain .09–.15/.20–.32/.08–.13 m; tiny uses .025–.05/.055–.10/.025–.045 m; oversized uses .18–.24/.36–.42/.14–.19 m. The longest resting model stays above the inspected world-body bottom. F7/pause shuffle now re-rolls axis as well, so a solo player can test both directions. A random roll can repeat the previous axis. All clients should run 1.5.3 to derive matching appearances. Left/right hinge movement responds especially to lateral acceleration, while forward/back responds to fore/aft acceleration.


## Body visual follow-up (1.5.4)

The hanging model now uses size-dependent root depth: BodyModel Z = .338 + .12 × depth, with Y remaining .03. Tiny models sit closer to the surface; normal models move a little back from the fixed .365 placement. Outward render tilt increases from 6 to 10 degrees. Tiny models use angular damping .25, linear damping .08 and maximum angular speed 14 rather than the standard 2/.35/6.

Forty percent of sideways appearance rolls allow unrestricted rotation around the existing local Z hinge. These use angular damping .12 and maximum angular speed 24 radians/sec. Forward/back rolls always retain ±55-degree limits; other sideways rolls do too. There is no motor, spring, scripted spin or added player force. Timed alternating strafes can supply energy to the free hinge. F7/pause shuffle re-rolls size, axis, free-rotation choice and tip profile. Both profiles remain simple eight-sided silhouettes with a slightly slimmer shaft and either a slender tip transition or broader rounded tip. No detailed anatomy or additional artwork.

LeftLeg/RightLeg now have simple .23 × .25 × .23 m sphere visuals attached at BodyModel X=-.34/+.34, Y=-.23, Z=0. Each follows its existing synchronized custom ownership state independently, waits for a known snapshot, and respects original body renderer visibility. Both use a copied plain body material and the body property block. No colliders, rigidbodies, new body states or economy changes are introduced. The local first-person render adjustment applies to these orbs too; remote and Cameraman views use the real body attachment. Existing controller cleanup destroys orbs/material on model replacement, scene change or unload, independently of Dong removal. All modded clients should update to 1.5.4.

Build/appearance metadata checks do not execute Unity physics. Confirm tiny swing response, root placement, both tip profiles, leg sell/restore visibility, and movement-driven full rotations in game.


## Leg placement and stumps (1.5.5)

Owned leg orbs move toward the center and slightly lower: BodyModel X=±.28, Y=-.285, Z=.04, retaining .23 × .25 × .23 m size. Missing legs now retain the same orb, mostly recessed at X=±.215, Y=-.285, Z=.02, colored deep blood red (.65,.012,.02). Each renderer starts from the original skin property block; only the missing leg receives a color override. Buying/restoring immediately restores its normal placement and skin color. Unknown initial state still shows neither orb. First-person adjustment, original renderer visibility, cleanup and existing authoritative state are retained. No gameplay, economy, networking or Dong physics changes. Live stump visibility/placement requires inspection.


## Machine clipping and interaction text (1.5.6)

The two-column button grid shifts .14 units toward the outside edge, with column spacing reduced from .40 to .32 in Model/Base coordinates. Centers now X=-2.20/-1.88; scale and row heights remain unchanged. Both faces of the original two-sided button hierarchy move together, preserving colliders, hover and press feedback. Verify the inner face against the wall and door frame in game.

All six interaction titles now use the part name (EYE/EAR/MOUTH/DONG/LEG/BODY); prompts consistently use Buy [part] or Sell [part] from the existing synchronized machine view. Vanilla EYE/MOUTH/BODY no longer retain Shred or Shredder text. A comparison-only late refresh corrects delayed vanilla localization without repeatedly notifying unchanged hover UI. Original prompts, positions and scales restore on unload. Prices, currency, transaction callbacks and server authority are unchanged.


## Dong nub, machine re-roll and blindness slice (1.5.7)

Missing Dong retains a small blood-red sphere nub derived from its latest synchronized appearance: width/depth 42% and length 12% of that model, recessed at its size-dependent root. Sale preserves the appearance counter, so the nub matches the removed model. Successful machine purchase increments that player's host appearance counter and broadcasts it through the existing cosmetic transport; future removal uses the new dimensions. Failed/rejected transactions do not re-roll. DONG already buys and sells for the same 2-ticket value, making sell/buy cycles currency neutral. F7 still re-rolls present Dong. The nub has no collider/physics and shares the existing copied plain material, with per-renderer red override; cleanup and first-person positioning match other body cosmetics.

Vanilla PlayerOrgans.UserCode_RpcSetEyes__Boolean__Boolean hides the serialized leftEyeModel/rightEyeModel pupil objects independently. The inspected Eye Left/Right hierarchies retain their M_Eyeball and other eye meshes; there is no separate missing-eye texture swap in this method. A narrow postfix reapplies both object states even when vanilla's cached flags skip the update, preserving its appearance for both missing eyes and repairing repeated-snapshot visual resets. No eye assets are replaced.

Both-eye blindness now masks across screen Y, with a 9%-height central plateau and soft feather to black out to 32% total height. It therefore gives a horizontal blurry slice rather than a vertical one. The existing center visibility setting, darkness strength, blur blend, eighth-resolution sampling and four Gaussian passes are unchanged; single-eye masks stay unchanged. Screen UVs remain matched to the mesh coordinates. The reported top/bottom mirrored impression has not been verified as an actual rendering fault, and blur sampling was not altered. Live visual inspection remains required.


## Full-blind orientation and softer slice (1.5.8)

With both eyes missing at the existing default full-blur strength (1), the blurred render texture is now copied back with URP Blitter.BlitCameraTexture rather than sampled by a UI mesh. This uses the game's normal fullscreen orientation path, avoiding the suspected inverted world presentation. Mouse input/invert settings remain untouched. Custom partial blur strengths retain the existing alpha-blended mesh path. Actual input inversion was ruled out by source inspection; the reported visual inversion still needs live confirmation.

The horizontal slice grows from a 9% to 11% central plateau, with a broader feather reaching black at 42% total height instead of 32%. The feather uses smootherstep with zero first and second derivatives at either end. Center visibility, blur resolution/passes/strength and single-eye settings remain unchanged.


## Underwater ears, visible ears and Butt (1.6.0)

Missing-side hearing now retains 75% gain with a 1.1 kHz low-pass cutoff; both missing retain 65% gain with 800 Hz cutoff. The directional region fades across the midpoint up to side-dot .25 on the intact side; exponent 1.5 smooths it. Existing FMOD world/voice and Unity sample-filter paths apply the filtering. Exact former shipped defaults (.20/800/.15/650/exponent 2) map to these new values at runtime so existing configs receive the requested change; other custom values remain in effect. No external config file was edited during development.

Two simple ellipsoid ears attach to the inspected Player/Model/PlayerHead/HeadModel, whose sphere radius is about .517 in those local coordinates (head scale .8). Owned ear centers X=±.54, Y=-.025; missing ear remnants move to ±.484 and shrink, with per-renderer blood-red tint. Each follows its existing LeftEar/RightEar state. Local first-person ears are hidden; remote/Cameraman rendering respects the original head renderer. No colliders or head/camera changes.

Butt is appended to custom body state as bit 5, independent of Dong and legs. Complete mask is 63; body protocol increments to 3, so all players must update together. Two collider-free ellipsoid cheeks attach behind BodyModel. Missing Butt removes both cheeks. BUTT adds a seventh machine entry on the fourth row, buy/sell both 2 tickets, reusing the existing host transaction pipeline, prices, mode prompts and feedback. No vanilla OrganType extension.

Press F or Escape → Fart. The host checks the requesting connection's own canonical Butt state and applies a one-second cooldown, then sends one positional sound event to subscribed authenticated ready clients (including host). Clients cannot choose an actor or grant themselves Butt. The local button disables when Butt is missing; typing into a focused TMP input does not trigger F. Sound plays from behind the player's body and uses the missing-ear hearing rules.

Reconnect/readiness subscription, scene/unload cleanup and audio cleanup are included. Pure tests cover all 64 ownership states, Butt single-charge buy/sell, unknown/missing state fart rejection, hearing bounds, symmetry and cross-midpoint reach. Build/metadata checks do not execute Unity rendering, sound or multiplayer. Inspect ear positions and red slivers, cheek placement, machine seventh button, F sound and ownership rejection with two updated clients.

## Supplied fart clips (1.6.1)

Each accepted F/button press chooses uniformly among the six user-supplied clips. Repeats are possible. The host chooses after the ownership/cooldown check and sends the clip index with the player's network identity, so every updated client hears the same selection once. Fart protocol is now 2; all players should update their DLL.

Clips are converted from the original MP3 downloads to mono 44.1 kHz PCM16 WAV and embedded in the DLL under Audio/Farts. The originals are unchanged, and no external audio files or decoder are needed at runtime. Spatial playback, underwater hearing effects, Butt ownership and the one-second cooldown are preserved.

Build and resource checks validate all six embedded clips, PCM decoding and truncated-data rejection. Unity audio playback and multiplayer still require an in-game check.

## Placement, stronger muffling and fart playback repair (1.6.2)

Cheeks move up .06 local units, centers narrow to X=±.10, and size shrinks to (.22,.25,.18). Leg orbs move .06 toward the front, including their recessed red remnants. Missing-ear defaults now use gain .65/cutoff 750 Hz on the affected side and .55/550 Hz for both missing, with a 1.25 directional exponent. Previous shipped defaults map to these settings at runtime; other custom values are preserved.

The live 1.6.1 log showed AudioClip.SetData failing with no data, despite requests reaching playback. FartAudio now loads embedded WAVs directly with the installed FMOD core, using OPENMEMORY copying and cached sounds. Each accepted message starts one positional channel, follows the player's body, applies the same ear gain/lowpass through an owned DSP chain, and cleans up when playback completes or the player disappears. No Unity AudioClip/AudioSource is needed. Playback/failure logging is included. Host selection, protocol 2, Butt validation and cooldown are unchanged.

Native FMOD NOSOUND checks pass for all six WAVs, copied-buffer lifetime, decoded durations, channel creation, spatial settings, lowpass attachment and cleanup. Pure hearing bounds/symmetry checks and the build pass. These checks do not verify audible output or placement in a running game.

## Missing-Butt puff and wounds (1.6.3)

F and the pause-menu Fart button now accept attempts with or without Butt, once authoritative body state is known. The host chooses the six normal clips only when Butt is present. When Butt is missing, it selects the supplied freesound_community-dry-puff-39175 clip and rolls pitch from .92 to 1.08 in .01 steps. The clip/pitch are broadcast together so every client hears the same result. The one-second cooldown, connection ownership and positional hearing effects remain. Fart protocol is now 3; update all players' DLLs. The dry puff is embedded in the DLL with the other clips.

Missing Butt now leaves two smaller recessed blood-red caps at the cheek attachment sites, replacing the owned cheeks and returning to normal skin when restored. The missing-Dong nub moves .006 local units farther into the body without changing its size-dependent appearance.

Checks cover all 64 body ownership masks, unknown-state rejection, seven embedded clips, native FMOD dry-puff playback and all 17 pitch settings. Debug build passes. NOSOUND testing does not verify audible output or live visual placement.
