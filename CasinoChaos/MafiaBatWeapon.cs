using HarmonyLib;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal enum MafiaWeaponType { Bat, Gun, OrganGun }

    internal interface IMafiaWeapon
    {
        MafiaWeaponType Type { get; }
        float AttackRange { get; }
        float Cooldown { get; }
        float Windup { get; }
        bool CanBeginAttack(NPC guard, PlayerController target);
        bool CanContinueAttack(NPC guard, PlayerController target);
        void Aim(NPC guard, PlayerController target);
        bool TryAttack(NPC guard, PlayerController target, Vector3 committedDirection);
    }

    internal sealed class MafiaBatWeapon : IMafiaWeapon
    {
        public MafiaWeaponType Type => MafiaWeaponType.Bat;
        public float AttackRange => 2.4f;
        public float Cooldown => 1.3f;
        public float Windup => 0.45f;
        private readonly float _power = 7f, _upPower = 7f, _randomPower = 3f, _torquePower = 10f;

        public bool CanBeginAttack(NPC guard, PlayerController target) =>
            Vector3.Distance(guard.transform.position, target.transform.position) <= AttackRange;
        public bool CanContinueAttack(NPC guard, PlayerController target) => true;
        public void Aim(NPC guard, PlayerController target) { } // Melee commits at windup start.

        internal MafiaBatWeapon()
        {
            // Read actual installed bat settings, including serialized overrides.
            var bats = Resources.FindObjectsOfTypeAll<Bat>();
            if (bats.Length == 0) return;
            var bat = bats[0];
            _power = AccessTools.Field(typeof(Bat), "power").GetValue(bat) is float p ? p : _power;
            _upPower = (float)AccessTools.Field(typeof(Bat), "upPower").GetValue(bat);
            _randomPower = (float)AccessTools.Field(typeof(Bat), "randomPower").GetValue(bat);
            _torquePower = (float)AccessTools.Field(typeof(Bat), "torquePower").GetValue(bat);
        }

        public bool TryAttack(NPC guard, PlayerController target, Vector3 direction)
        {
            Vector3 offset = target.transform.position - guard.transform.position;
            Vector3 flat = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (!BatStrikeRules.CanReach(flat.magnitude, offset.y, Vector3.Angle(direction, flat), AttackRange) || target.IsLocked ||
                target.State != PlayerController.PlayerState.Free) return false;
            var body = target.GetComponent<Rigidbody>();
            if (!body) return false;
            Vector3 contact = body.worldCenterOfMass;
            Vector3 strikeOrigin = guard.transform.position + Vector3.up;
            foreach (var hit in Physics.RaycastAll(strikeOrigin, (contact - strikeOrigin).normalized,
                Vector3.Distance(strikeOrigin, contact), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<PlayerController>() != target
                    && hit.collider.GetComponentInParent<NPC>() != guard) return false;
            // Same force/torque formula as Bat's server player-hit body (falldown=1).
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            Vector3 force = direction * _power + Vector3.up * _upPower +
                right * Random.Range(-1f, 1f) * _randomPower;
            Vector3 strikePoint = strikeOrigin + direction * 0.75f;
            Vector3 torque = Vector3.Cross(Vector3.ClampMagnitude(strikePoint - contact, 1f), direction) * _torquePower;
            target.ServerKnockback(force, torque);
            return true;
        }
    }
}
