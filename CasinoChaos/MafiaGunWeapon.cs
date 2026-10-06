using System;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal sealed class MafiaGunSettings
    {
        internal float PreferredRange = 10f;
        internal float MinimumRange = 4.5f;
        internal float MaximumRange = 18f;
        internal float FireCooldown = 1.6f;
        internal float AimDelay = 0.45f;
        internal float SpreadDegrees = 10f;
        internal float CloseSpreadDegrees = 18f;
    }

    internal sealed class MafiaGunWeapon : IMafiaWeapon
    {
        private readonly MafiaGunSettings _settings;
        private readonly QuotaGun _vanilla;
        private readonly Renderer _gunMesh;
        private readonly LayerMask _mask;
        private readonly float _shotRange;
        private readonly ParticleSystem _flash;
        private readonly LineRenderer _tracer;
        private readonly GameObject _impact;
        private readonly object _shootSound, _playerSound, _worldSound;
        private static readonly MethodInfo Force = AccessTools.Method(typeof(QuotaGun), "CalculateKnockbackVector");
        private static readonly MethodInfo Torque = AccessTools.Method(typeof(QuotaGun), "CalculateTorque");
        private static readonly MethodInfo Sound = AccessTools.Method(typeof(SFXManager), "SFXOneShot");
        public MafiaWeaponType Type => MafiaWeaponType.Gun;
        public float AttackRange => _settings.MaximumRange;
        public float Cooldown => _settings.FireCooldown;
        public float Windup => _settings.AimDelay;

        internal MafiaGunWeapon(NPC guard, MafiaGunSettings settings)
        {
            _settings = settings;
            // Use a vanilla resource prefab as read-only data. Never instantiate
            // its inventory, QuotaGun or NetworkBehaviour components on the NPC.
            var spawnables = Resources.Load<SpawnableSettings>("SpawnableSettings");
            if (spawnables)
                foreach (var entry in spawnables.spawnables)
                    if (entry && entry.prefab && entry.prefab.TryGetComponent<QuotaGun>(out var gun))
                    { _vanilla = gun; break; }
            if (!_vanilla)
                foreach (var gun in Resources.FindObjectsOfTypeAll<QuotaGun>())
                    if (!gun.gameObject.scene.IsValid()) { _vanilla = gun; break; }
            if (!_vanilla) throw new InvalidOperationException("Vanilla QuotaGun prefab unavailable; refusing guessed gun settings.");
            _gunMesh = guard.transform.Find("Model/NpcModel/NewBody/Cosmetics/SharkGoon/SM_Icon_Gun_01")?.GetComponent<Renderer>();
            if (!_gunMesh) throw new InvalidOperationException("SharkGoon gun mesh unavailable.");
            _mask = Field<LayerMask>("rayMask");
            _shotRange = Mathf.Min(Field<float>("raycastDistance"), settings.MaximumRange);
            _flash = Field<ParticleSystem>("muzzleVFX");
            _tracer = Field<LineRenderer>("lineRenderer");
            _impact = Field<GameObject>("hitVFX");
            _shootSound = Field<object>("shootSfx");
            _playerSound = Field<object>("hitPlayerSfx");
            _worldSound = Field<object>("hitRandomSfx");
            CasinoChaosPlugin.Log($"Gun initialized: vanilla='{_vanilla.name}' mask={_mask.value} range={_shotRange:F1} " +
                $"preferred={settings.PreferredRange} spread={settings.SpreadDegrees} muzzle=mesh front '{_gunMesh.name}' " +
                "presentation=host-only vanilla VFX/tracer/FMOD; no firing clip on goon; vanilla clients receive movement/knockback.");
        }

        private T Field<T>(string name) => (T)AccessTools.Field(typeof(QuotaGun), name).GetValue(_vanilla);
        private Vector3 Muzzle(NPC guard)
        {
            var b = _gunMesh.bounds;
            var forward = guard.transform.forward;
            float radius = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)));
            return b.center + forward * (radius + 0.03f);
        }
        private static Vector3 TargetPoint(PlayerController target) => target.GetComponent<Rigidbody>().worldCenterOfMass;

        // RaycastAll avoids the vanilla eight-hit buffer truncating crowded rays.
        // Retain vanilla mask, Ignore triggers, nearest obstruction, attached RB.
        private bool Trace(NPC guard, Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, _mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(guard.transform)) continue;
                if (!found || hit.distance < nearest.distance) { found = true; nearest = hit; }
            }
            return found;
        }
        private static bool IsTarget(RaycastHit hit, PlayerController target) =>
            hit.collider && hit.collider.attachedRigidbody &&
            hit.collider.attachedRigidbody.GetComponent<PlayerController>() == target;

        private bool Visible(NPC guard, PlayerController target)
        {
            if (!target.GetComponent<Rigidbody>()) return false;
            Vector3 muzzle = Muzzle(guard), point = TargetPoint(target);
            // Also check the segment from the guard to the mesh front: a muzzle
            // protruding through a wall must not bypass that wall.
            Vector3 chest = guard.transform.position + Vector3.up;
            Vector3 offset = muzzle - chest;
            if (Trace(guard, chest, offset.normalized, offset.magnitude, out var obstruction) && !IsTarget(obstruction, target)) return false;
            offset = point - muzzle;
            return Trace(guard, muzzle, offset.normalized, offset.magnitude + 0.1f, out var hit) && IsTarget(hit, target);
        }

        public bool CanBeginAttack(NPC guard, PlayerController target) =>
            Vector3.Distance(guard.transform.position, target.transform.position) <= _settings.PreferredRange && Visible(guard, target);
        public bool CanContinueAttack(NPC guard, PlayerController target) =>
            target.State == PlayerController.PlayerState.Free && !target.IsLocked &&
            Vector3.Distance(guard.transform.position, target.transform.position) <= _shotRange && Visible(guard, target);
        public void Aim(NPC guard, PlayerController target)
        {
            Vector3 forward = Vector3.ProjectOnPlane(target.transform.position - guard.transform.position, Vector3.up);
            if (forward.sqrMagnitude > 0.001f) guard.transform.rotation = Quaternion.LookRotation(forward);
        }

        public bool TryAttack(NPC guard, PlayerController target, Vector3 committedDirection)
        {
            if (!NetworkServer.active) return false;
            if (!CanContinueAttack(guard, target))
            { CasinoChaosPlugin.Log("Gun shot cancelled: range/state/line of sight blocked; pursue again."); return false; }
            Aim(guard, target);
            Vector3 origin = Muzzle(guard);
            Vector3 desired = (TargetPoint(target) - origin).normalized;
            float spread = Vector3.Distance(guard.transform.position, target.transform.position) < _settings.MinimumRange
                ? _settings.CloseSpreadDegrees : _settings.SpreadDegrees;
            // Uniform solid-angle cone, oriented in an orthonormal aim basis.
            GunSpreadRules.Sample(spread, UnityEngine.Random.value, UnityEngine.Random.value, out float x, out float y, out float z);
            Vector3 local = new Vector3(x, y, z);
            Vector3 direction = (Quaternion.LookRotation(desired) * local).normalized;
            bool contact = Trace(guard, origin, direction, _shotRange, out var hit);
            Vector3 endpoint = contact ? hit.point : origin + direction * _shotRange;
            bool playerHit = contact && IsTarget(hit, target);
            CasinoChaosPlugin.Log($"Gun shot fired: guard={guard.netId} offender={HeatSystem.Identity(target)} host={NetworkClient.active} " +
                $"server={NetworkServer.active} origin={origin} spread={spread:F1} direction={direction} " +
                $"raycast={(contact ? hit.collider.name : "MISS")} point={endpoint}");
            // A local effect failure must never change the authoritative hit.
            try { Present(origin, direction, endpoint, contact, playerHit); }
            catch (Exception error) { CasinoChaosPlugin.Log("Gun local presentation failed: " + error); }
            if (!playerHit)
            { CasinoChaosPlugin.Log(contact ? "Gun shot blocked/hit another object; no knockback." : "Gun shot missed; no knockback."); return false; }
            var body = target.GetComponent<Rigidbody>();
            Vector3 force = (Vector3)Force.Invoke(_vanilla, new object[] { direction });
            Vector3 torque = (Vector3)Torque.Invoke(_vanilla, new object[] { hit.point, body.worldCenterOfMass, direction });
            target.ServerKnockback(force, torque);
            CasinoChaosPlugin.Log($"Gun player hit; vanilla ServerKnockback applied force={force} torque={torque}; no organ path.");
            return true;
        }

        private void Present(Vector3 origin, Vector3 direction, Vector3 endpoint, bool contact, bool playerHit)
        {
            if (!NetworkClient.active) return; // Presentation is local to a listening host.
            if (_flash)
            {
                var flash = UnityEngine.Object.Instantiate(_flash, origin, Quaternion.LookRotation(direction));
                flash.gameObject.SetActive(true);
                flash.Play(true);
                UnityEngine.Object.Destroy(flash.gameObject, 2f);
            }
            if (_tracer)
            {
                var line = UnityEngine.Object.Instantiate(_tracer);
                line.gameObject.SetActive(true); line.enabled = true; line.useWorldSpace = true; line.positionCount = 2;
                line.SetPosition(0, origin); line.SetPosition(1, endpoint);
                UnityEngine.Object.Destroy(line.gameObject, 0.05f);
            }
            Sound.Invoke(null, new object[] { _shootSound, origin });
            if (contact)
            {
                if (_impact) UnityEngine.Object.Destroy(UnityEngine.Object.Instantiate(_impact, endpoint, Quaternion.identity), 1f);
                Sound.Invoke(null, new object[] { playerHit ? _playerSound : _worldSound, endpoint });
            }
            CasinoChaosPlugin.Log("Gun presentation triggered: local vanilla muzzle flash/tracer/shot SFX/impact; remote effects unavailable.");
        }
    }
}
