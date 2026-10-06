using Extensions;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    [HarmonyPatch(typeof(Bat), "UserCode_CmdHitNpc__NPC__Single", new System.Type[] { typeof(NPC), typeof(float) })]
    internal static class GuardBatDefeat
    {
        private static void Postfix(Bat __instance, NPC npc)
        {
            if (!NetworkServer.active || !npc || npc.State != NPC.NPCState.Ragdoll) return;
            var guard = npc.GetComponent<MafiaGuardController>();
            if (!guard || guard.IsDefeated || !npc.GetComponent<Rigidbody>()) return;
            var holder = __instance.NetworkHolder;
            var attacker = holder ? holder.GetComponent<PlayerController>() : null;
            if (!attacker) return;
            // This is exclusively the successful vanilla bat-vs-NPC server path.
            // Generic knockbacks (including Quota Gun) do not enter this postfix.
            guard.DefeatFromBat(attacker);
        }
    }

    [HarmonyPatch(typeof(NPC), nameof(NPC.State), MethodType.Setter)]
    internal static class DefeatedGuardRecovery
    {
        private static bool Prefix(NPC __instance, NPC.NPCState value)
        {
            if (!NetworkServer.active || value != NPC.NPCState.Free) return true;
            var guard = __instance.GetComponent<MafiaGuardController>();
            if (!guard || !guard.IsDefeated) return true;
            // Block only the corpse's automatic recovery. The original Ragdoll
            // SyncVar and force RPC remain intact on unmodded clients.
            guard.LogBlockedRecovery();
            return false;
        }
    }

    internal static class GuardDefeatDiagnostics
    {
        private static readonly AccessTools.FieldRef<NPCSpawner, SyncList<NPC>> Civilians =
            AccessTools.FieldRefAccess<NPCSpawner, SyncList<NPC>>("NPCs");

        internal static void Log(NPC npc)
        {
            CasinoChaosPlugin.Log("Vanilla ragdoll path completed: " + Snapshot(npc));
            var spawner = NetworkSingleton<NPCSpawner>.Instance;
            if (!spawner) return;
            foreach (var civilian in Civilians(spawner))
                if (civilian && civilian.gameObject.activeInHierarchy)
                {
                    CasinoChaosPlugin.Log("Runtime civilian comparison: " + Snapshot(civilian));
                    break;
                }
        }

        private static string Snapshot(NPC npc)
        {
            var rb = npc.GetComponent<Rigidbody>();
            var animator = npc.GetComponent<Animator>();
            var networkAnimator = npc.GetComponent<NetworkAnimator>();
            var agent = npc.Agent;
            var crowd = NetworkSingleton<NPCController>.Instance;
            return $"npc='{npc.name}' netId={npc.netId} state={npc.State}; " +
                $"Rigidbody gravity={rb.useGravity} kinematic={rb.isKinematic} constraints={rb.constraints}; " +
                $"NavMeshAgent enabled={(agent && agent.enabled)}; " +
                $"Animator enabled={(animator && animator.enabled)} rootMotion={(animator && animator.applyRootMotion)}; " +
                $"NetworkAnimator present={(bool)networkAnimator}; " +
                $"rigidbodyCount={npc.GetComponentsInChildren<Rigidbody>(true).Length} " +
                $"colliderCount={npc.GetComponentsInChildren<Collider>(true).Length}; " +
                $"NPCController registered={(crowd && crowd.GetNPCState(npc) != null)}";
        }
    }
}
