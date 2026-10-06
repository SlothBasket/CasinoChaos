# Historical notes — before the audio/body-part pass

These notes describe earlier host-only passes and their original testing status. They are retained as historical context; consult the repository README for the current multiplayer requirement.

# CasinoChaos: first mafia prototype

Build the development DLL with `dotnet build -c Debug`. The existing build target copies only `GWYF_CasinoChaos.dll` into the game's `BepInEx/gwyf_mods` directory. A Release build omits the F9 development shortcut.

Accepted civilian bat hits add $5 to the shared balance and +1 heat to the actual offending player, including remote players. At heat 3 the host spawns one guard. The short existing 0.15-second bat/NPC duplicate window applies to both reward and heat.

Heat uses server player netId. Records clear on disconnect, server stop, scene change, or mod unload. There is no timed decay. A guard response occurs once per offender per scene; destroying that guard or moving floors does not create more waves. NPC floor cleanup can destroy a guard.

## Confirmed vanilla assets

The installed `LoseStateScene` (level6) contains `Players Grave` and four `NPC Static` characters using cosmetic preset 1 (`SharkGoon`). They include Rigidbody, CapsuleCollider, Animator, NPCCosmeticSelector, UIColorManager and NavMeshObstacle, but no NetworkIdentity, NPC, NavMeshAgent or NetworkAnimator. Their goon assets include `SharkGoon` and `MafiaNPC_Attire`.

The same SharkGoon appearance exists in `resources.assets` on `NPC Dynamic`. This prefab includes NetworkIdentity, NPC, NavMeshAgent, Rigidbody, CustomDrag, CapsuleCollider, Animator, NetworkAnimator, RandomNPCSFX, NPCCosmeticSelector and UIColorManager. It has no NetworkTransform component.

`MafiaGuardSpawner` uses `NPCSpawner.npcPrefab` and looks up the named SharkGoon preset instead of assuming its index at runtime. It applies the existing `NPCSpawner.RpcSetupNPC` to synchronize the floor parent and appearance. The guard is excluded from the spawner's civilian list, so hitting the guard cannot award civilian money or heat.

## AI and combat

The controller is an ordinary host-only MonoBehaviour, preserving the vanilla prefab's NetworkBehaviour layout. The host removes it from NPCController crowd updates. UnregisterNPC only removes the AI list/state; a scoped RegisterNPC prefix prevents this guard's delayed registration from enabling crowd behavior again. Normal civilians are unaffected.

Idle -> Chasing -> Attacking -> Recovering -> Chasing. Destination refresh: 0.3 seconds. Bat reach: 2.4 m. Wind-up: 0.45 seconds. Attack interval: at least 1.3 seconds. The committed attack direction is checked at impact time, so movement can dodge the strike. Walls/other intervening colliders block a strike, and already-ragdolled or locked players are not attacked.

Force/torque follows the vanilla Bat server player-hit formula, reading installed Bat settings where available. PlayerController.ServerKnockback performs the normal held-item drop and player ragdoll. No organ-removal path is called. Gun and OrganGun are enum values only; no shooting is implemented.

## Visual/network limits

The guard has the vanilla SharkGoon appearance. There is no attached visible bat or custom attack animation in this pass: no NPC hand-attachment RPC was verified, and a host-only clone would not be visible to vanilla clients. The guard's weapon is currently server bat combat only.

NPC movement uses SetDestination and its existing destination RPC, so clients simulate navigation rather than receiving a NetworkTransform. Stops are also sent as destinations. Exact facing/positional drift needs a real multiplayer test; no custom client network component was introduced. Existing NetworkAnimator and NPC ragdoll RPCs remain intact.

## Solo smoke test

1. Restart/reload CasinoChaos after building. Host a solo game and enter a casino floor using the normal elevator, ensuring the floor NavMesh has initialized.
2. In this Debug build, press F9 to set the local host's heat to 3 without changing money. Alternatively, land three separate bat hits on ordinary civilians: each should award $5 and one heat.
3. Confirm exactly one SharkGoon appears around 6 m away, follows you, pauses for wind-up, and sends normal bat-style knockback when you remain in range/in front.
4. Kite outside 2.4 m or dodge sideways/behind during wind-up; confirm missed attempts and no knockback. Test a wall between you and the guard.
5. Hit the guard: no civilian reward/heat. Continue hitting civilians: heat rises, but no additional guards appear.

## Multiplayer smoke test (pending)

With an unmodded friend connected, have that friend attack civilians three times. Confirm $5 per hit total, heat assigned to the friend, one visible SharkGoon assigned only to that friend, destination/stop synchronization, and normal target-client knockback. Innocent players should not become targets. Check disconnect cleanup and scene/floor transitions.

Temporary logs cover civilian heat changes, identity/netId/Steam ID, money before/after, spawn prefab/appearance/position, state changes/distances, throttled destinations, attack attempts/hit or miss, invalid targets, and despawn.

GameReference remains excluded by the workspace .gitignore. Do not edit or commit the decompiled source.

## Guard defeat pass

One accepted player baseball-bat hit now permanently transitions the guard to MafiaGuardState.Defeated. Both host and remote player bat hits qualify; generic collisions, falls, and Quota Gun knockback do not. The original Bat.UserCode_CmdHitNpc__NPC__Single already calls NPC.ServerKnockback, so the defeat postfix adds no second impulse and preserves the original hit direction/strength.

The custom controller is disabled. A guard-only NPC.State setter prefix rejects recovery to Free while defeated, leaving the server Ragdoll SyncVar and original ApplyKnockbackRpc intact for vanilla clients. Animator, NetworkAnimator, Rigidbody gravity/kinematic settings, and collider layout are not manually changed. Existing civilian recovery is unaffected.

The named MafiaGuardLifecycle.DefeatedLifetimeSeconds constant is 60 real seconds. Additional bat hits do not extend it. The corpse's coroutine remains active on the disabled controller and uses NetworkServer.Destroy at expiry. Disconnecting the offender leaves the corpse's timer intact. Normal scene/floor teardown or mod unload can still clean it up earlier. Heat does not decrease and the existing Responded flag is unchanged, so defeat does not create a replacement wave.

Asset checks confirm NPC Dynamic (used for both civilian and guard) has one root Rigidbody, gravity=true, isKinematic=false, FreezeRotation constraints at rest, one non-trigger CapsuleCollider, Animator with root motion=false, NetworkAnimator, and no child rigidbodies. Vanilla ragdoll releases root rotation constraints and disables navigation; it is whole-body tumbling, not a segmented skeletal rig. Live sliding/floating has not been reproduced during this change. Defeat logs include the actual guard physics state and a current civilian snapshot for comparison.

Test: F9 in a casino floor -> hit the guard once with your bat -> verify Defeated, tumbling/knockback, no later chase/attack, and removal around 60 seconds. Hit it again during those 60 seconds and confirm the original deadline remains. Check ordinary civilian recovery/$5/heat, remote-player bat defeat on an unmodded client, and gun/collision knockback without defeat. Check logs for gravity, constraints=None, navigation disabled, and Rigidbody/collider counts.

Validation: Debug build passed with zero warnings/errors and was deployed through the existing DLL target. Twelve lifecycle checks passed, covering terminal state, no attack/chase/recovery after defeat, unchanged deadline on repeated hits, and the 60-second boundary. Game Assembly-CSharp.dll hash remains unchanged. Actual in-game ragdoll and unmodded-client synchronization still need a smoke test.


## Ranged gun pass

Heat response now spawns MafiaWeaponType.Gun. MafiaGunSettings provides 10 m preferred / 18 m maximum / 4.5 m minimum range, 0.45 s aim, 1.6 s cooldown, 10 degree cone spread (18 degrees close). Host raycasts using the vanilla QuotaGun mask and Ignore triggers; nearest collision must be the offender before ServerKnockback. The actual prefab CalculateKnockbackVector/CalculateTorque supply force and torque. No organ logic. Bat weapon behavior and permanent guard defeat remain available.

The SharkGoon gun is a mesh only. Host effects reuse vanilla QuotaGun flash/tracer/impact and FMOD assets locally. Vanilla clients receive NPC destination movement and player knockback but cannot see/hear these local shot effects or exact aiming rotation. Holder-gated QuotaGun RPCs are not forced or patched. No functional inventory gun or custom NetworkBehaviour is added.

Debug built/deployed; spread math, bat reach and defeat lifecycle checks passed. Live muzzle placement, collision, sound and multiplayer still need testing. F9 during an active casino game spawns the heat response for the host (Debug only).
