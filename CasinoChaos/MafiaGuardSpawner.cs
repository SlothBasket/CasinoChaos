using System;
using System.Reflection;
using Extensions;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace GWYF_CasinoChaos
{
    internal static class MafiaGuardSpawner
    {
        private static readonly MethodInfo SetupRpc = AccessTools.Method(typeof(NPCSpawner), "RpcSetupNPC");
        private static readonly AccessTools.FieldRef<NPCCosmeticSelector, NPCCosmeticSelector.CosmeticPreset[]> Presets =
            AccessTools.FieldRefAccess<NPCCosmeticSelector, NPCCosmeticSelector.CosmeticPreset[]>("presets");

        internal static MafiaGuardController Spawn(PlayerController offender, MafiaWeaponType weaponType = MafiaWeaponType.Gun)
        {
            var spawner = NetworkSingleton<NPCSpawner>.Instance;
            if (!NetworkServer.active || !offender || !spawner || !spawner.npcPrefab) return null;
            // Active floor + local NavMesh sampling prevents spawning onto an
            // inactive floor or snapping through the building to another level.
            CasinoFloor floor = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<CasinoFloor>(FindObjectsSortMode.None))
                if (candidate.gameObject.activeInHierarchy && candidate.GetComponentInChildren<NPCHolder>())
                { floor = candidate; break; }
            if (!floor) return null;
            Vector3 position = Vector3.zero;
            bool found = false;
            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = Quaternion.Euler(0, i * 45, 0) * Vector3.forward * 6f;
                if (NavMesh.SamplePosition(offender.transform.position + offset, out var hit, 2f, NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.y - offender.transform.position.y) < 2f
                    && Vector3.Distance(hit.position, offender.transform.position) >= 4f)
                { position = hit.position; found = true; break; }
            }
            if (!found)
            {
                CasinoChaosPlugin.Log("Guard spawn deferred: no nearby floor NavMesh position.");
                return null;
            }

            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(spawner.npcPrefab, position, Quaternion.identity);
                var npc = instance.GetComponent<NPC>();
                var selector = instance.GetComponent<NPCCosmeticSelector>();
                if (!npc || !npc.Agent || !instance.GetComponent<NetworkIdentity>())
                    throw new InvalidOperationException("Vanilla NPC prefab lacks required NPC/navigation/network components.");
                int preset = 0;
                string appearance = "vanilla NPC fallback";
                if (selector)
                {
                    var presets = Presets(selector);
                    for (int i = 0; i < presets.Length; i++)
                        if (presets[i] != null && presets[i].presetName == "SharkGoon")
                        { preset = i; appearance = "SharkGoon"; break; }
                    selector.SetSelectedPresetIndex(preset);
                }
                NetworkServer.Spawn(instance);
                // Use the existing registered NPC behaviour layout and setup RPC.
                // No custom NetworkBehaviour is added, so vanilla clients can spawn it.
                instance.transform.SetParent(floor.GetComponentInChildren<NPCHolder>().transform);
                SetupRpc.Invoke(spawner, new object[] { npc.netId, floor.floorIndex, preset });
                var guard = instance.AddComponent<MafiaGuardController>();
                IMafiaWeapon weapon;
                if (weaponType == MafiaWeaponType.Bat) weapon = new MafiaBatWeapon();
                else if (weaponType == MafiaWeaponType.Gun) weapon = new MafiaGunWeapon(npc, new MafiaGunSettings());
                else throw new NotSupportedException("OrganGun is not implemented.");
                guard.Initialize(npc, offender, floor, weapon);
                CasinoChaosPlugin.Log($"Guard spawned: prefab='{spawner.npcPrefab.name}' appearance={appearance} " +
                    $"weapon={weapon.Type} offender={HeatSystem.Identity(offender)} position={position}");
                return guard;
            }
            catch (Exception error)
            {
                if (instance)
                {
                    var identity = instance.GetComponent<NetworkIdentity>();
                    if (identity && identity.netId != 0) NetworkServer.Destroy(instance);
                    else UnityEngine.Object.Destroy(instance);
                }
                CasinoChaosPlugin.Log("Guard spawn failed: " + error);
                return null;
            }
        }
    }
}
