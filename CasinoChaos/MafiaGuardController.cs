using Extensions;
using Mirror;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using GuardState = GWYF_CasinoChaos.MafiaGuardState;

namespace GWYF_CasinoChaos
{
    // Host-only MonoBehaviour. The client's prefab keeps its original vanilla
    // NetworkBehaviour layout; movement/ragdoll/presentation use vanilla systems.
    internal sealed class MafiaGuardController : MonoBehaviour
    {
        private NPC _npc;
        private PlayerController _target;
        private CasinoFloor _floor;
        private IMafiaWeapon _weapon;
        private readonly MafiaGuardLifecycle _lifecycle = new MafiaGuardLifecycle();
        private GuardState _state => _lifecycle.State;
        internal bool IsDefeated => _lifecycle.IsDefeated;
        private bool _recoveryBlockedLogged;
        private float _nextDestination, _stateUntil, _nextAttack;
        private Vector3 _swingDirection;
        private bool _initialized, _despawning;
        private const float DestinationRefresh = 0.3f;
        private float _nextDestinationLog;

        internal void Initialize(NPC npc, PlayerController target, CasinoFloor floor, IMafiaWeapon weapon)
        {
            _npc = npc; _target = target; _floor = floor; _weapon = weapon;
            var crowd = NetworkSingleton<NPCController>.Instance;
            // Spawned fresh; OnStartServer may have registered it already. Register
            // interception below also handles a delayed RegisterWithController.
            if (crowd) crowd.UnregisterNPC(npc);
            npc.Initialize(3.5f, 0.5f);
            _initialized = true;
            CasinoChaosPlugin.Log($"Guard state=Idle assigned={HeatSystem.Identity(target)} distance={Distance():F1}");
        }

        private float Distance() => _target ? Vector3.Distance(transform.position, _target.transform.position) : -1f;

        private void Update()
        {
            if (!_initialized || !NetworkServer.active || _despawning || IsDefeated) return;
            if (!_target || !NetworkServer.spawned.ContainsKey(_target.netId))
            { Despawn("lost/invalid assigned target"); return; }
            if (!_floor || !_floor.gameObject.activeInHierarchy ||
                !NetworkSingleton<GameManager>.Instance || NetworkSingleton<GameManager>.Instance.state != GameState.Game)
            { Despawn("casino floor/round no longer active"); return; }
            var agent = _npc.Agent;
            if (_npc.State == NPC.NPCState.Ragdoll || !agent || !agent.enabled || !agent.isOnNavMesh)
            {
                SetState(GuardState.Recovering);
                _stateUntil = Time.time + 0.3f;
                return;
            }
            switch (_state)
            {
                case GuardState.Idle:
                    SetState(GuardState.Chasing);
                    break;
                case GuardState.Chasing:
                    if (_weapon.CanBeginAttack(_npc, _target))
                    {
                        _npc.SetDestination(transform.position);
                        agent.isStopped = true;
                        _weapon.Aim(_npc, _target);
                        if (Time.time < _nextAttack || _target.State != PlayerController.PlayerState.Free || _target.IsLocked) break;
                        _swingDirection = Vector3.ProjectOnPlane(_target.transform.position - transform.position, Vector3.up).normalized;
                        if (_swingDirection == Vector3.zero) _swingDirection = transform.forward;
                        transform.rotation = Quaternion.LookRotation(_swingDirection);
                        // A destination at the guard's position stops client agents too.
                        _npc.SetDestination(transform.position);
                        agent.isStopped = true;
                        _stateUntil = Time.time + _weapon.Windup;
                        _nextAttack = Time.time + _weapon.Cooldown;
                        SetState(GuardState.Attacking);
                        CasinoChaosPlugin.Log($"Guard acquired firing/attack position: weapon={_weapon.Type} distance={Distance():F1}; aim started delay={_weapon.Windup:F2}s");
                    }
                    else if (Time.time >= _nextDestination)
                    {
                        _nextDestination = Time.time + DestinationRefresh;
                        if (NavMesh.SamplePosition(_target.transform.position, out var hit, 1.5f, agent.areaMask))
                        {
                            agent.isStopped = false;
                            _npc.SetDestination(hit.position);
                            if (Time.time >= _nextDestinationLog)
                            {
                                _nextDestinationLog = Time.time + 2f;
                                CasinoChaosPlugin.Log($"Guard destination={hit.position} distance={Distance():F1} offender={HeatSystem.Identity(_target)}");
                            }
                        }
                    }
                    break;
                case GuardState.Attacking:
                    _weapon.Aim(_npc, _target);
                    if (!_weapon.CanContinueAttack(_npc, _target))
                    { CasinoChaosPlugin.Log($"Guard aim cancelled: weapon={_weapon.Type} target obscured/out of range/not free; resume pursuit.");
                        agent.isStopped = false; _nextDestination = 0; SetState(GuardState.Chasing); break; }
                    if (Time.time < _stateUntil) break;
                    CasinoChaosPlugin.Log($"Guard attack attempt: weapon={_weapon.Type} offender={HeatSystem.Identity(_target)} distance={Distance():F1}");
                    bool hitTarget = _weapon.TryAttack(_npc, _target, _swingDirection);
                    CasinoChaosPlugin.Log(hitTarget ? "Guard successful attack: vanilla player knockback sent." : "Guard attack missed/blocked; no knockback.");
                    _stateUntil = _nextAttack;
                    SetState(GuardState.Recovering);
                    break;
                case GuardState.Recovering:
                    _weapon.Aim(_npc, _target);
                    if (!_weapon.CanContinueAttack(_npc, _target))
                    { CasinoChaosPlugin.Log($"Guard firing position lost: weapon={_weapon.Type}; resume pursuit.");
                        agent.isStopped = false; _nextDestination = 0; SetState(GuardState.Chasing); break; }
                    if (Time.time >= _stateUntil)
                    { agent.isStopped = false; _nextDestination = 0; SetState(GuardState.Chasing); }
                    break;
            }
        }

        private void SetState(GuardState value)
        {
            var before = _state;
            if (!_lifecycle.TryTransition(value, Time.realtimeSinceStartupAsDouble)) return;
            CasinoChaosPlugin.Log($"Guard id={_npc.netId} state {before}->{value} distance={Distance():F1}");
        }

        internal void DefeatFromBat(PlayerController attacker)
        {
            if (!NetworkServer.active || IsDefeated || _despawning) return;
            CasinoChaosPlugin.Log($"Mafia guard hit by player bat: guardId={_npc.netId} attacker={HeatSystem.Identity(attacker)}");
            SetState(GuardState.Defeated);
            // The vanilla bat command has already called ServerKnockback. Do not
            // apply a second impulse or override the civilian physics settings.
            GuardDefeatDiagnostics.Log(_npc);
            enabled = false;
            CasinoChaosPlugin.Log($"Guard id={_npc.netId} custom MafiaGuardController AI disabled permanently; " +
                "NavMeshAgent disabled by vanilla NPC ragdoll; Animator/NetworkAnimator left as vanilla.");
            StartCoroutine(DespawnDefeated());
            CasinoChaosPlugin.Log($"Guard id={_npc.netId} defeated despawn timer started: {MafiaGuardLifecycle.DefeatedLifetimeSeconds}s realtime.");
        }

        private IEnumerator DespawnDefeated()
        {
            // Unity coroutines continue when this MonoBehaviour is disabled.
            // Use unscaled time so this is 60 real seconds, not 60 scaled seconds.
            while (!_lifecycle.ShouldDespawn(Time.realtimeSinceStartupAsDouble))
                yield return new WaitForSecondsRealtime((float)(_lifecycle.DespawnAt - Time.realtimeSinceStartupAsDouble));
            if (NetworkServer.active) Despawn("defeated lifetime elapsed");
        }

        internal void LogBlockedRecovery()
        {
            if (_recoveryBlockedLogged) return;
            _recoveryBlockedLogged = true;
            CasinoChaosPlugin.Log($"Guard id={_npc.netId} vanilla stand-up recovery blocked; remains NPCState.Ragdoll.");
        }

        internal static void DespawnAll(string reason)
        {
            foreach (var guard in Object.FindObjectsByType<MafiaGuardController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                guard.Despawn(reason);
        }

        internal void Despawn(string reason)
        {
            if (_despawning) return;
            _despawning = true;
            CasinoChaosPlugin.Log($"Guard id={(_npc ? _npc.netId : 0)} despawn: {reason}");
            if (NetworkServer.active) NetworkServer.Destroy(gameObject);
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (!_despawning && _initialized) CasinoChaosPlugin.Log("Guard despawn: vanilla floor cleanup/object destroyed.");
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(NPCController), nameof(NPCController.RegisterNPC))]
    internal static class GuardCrowdRegistration
    {
        private static bool Prefix(NPC npc) => !npc || !npc.GetComponent<MafiaGuardController>();
    }
}
