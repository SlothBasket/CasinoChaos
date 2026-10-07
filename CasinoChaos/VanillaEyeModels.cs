using HarmonyLib;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    // Keep vanilla socket/pupil assets: do not substitute an invented texture.
    [HarmonyPatch(typeof(PlayerOrgans),"UserCode_RpcSetEyes__Boolean__Boolean")]
    internal static class VanillaEyeModels
    {
        private static readonly AccessTools.FieldRef<PlayerOrgans,GameObject> Left=AccessTools.FieldRefAccess<PlayerOrgans,GameObject>("leftEyeModel");
        private static readonly AccessTools.FieldRef<PlayerOrgans,GameObject> Right=AccessTools.FieldRefAccess<PlayerOrgans,GameObject>("rightEyeModel");
        private static void Postfix(PlayerOrgans __instance,bool leftEye,bool rightEye)
        {
            // Vanilla skips its entire update when cached flags match. Reapply
            // each model even on repeated snapshots to repair visual resets.
            var left=Left(__instance);var right=Right(__instance);
            if(left&&left.activeSelf!=leftEye)left.SetActive(leftEye);
            if(right&&right.activeSelf!=rightEye)right.SetActive(rightEye);
        }
    }
}
