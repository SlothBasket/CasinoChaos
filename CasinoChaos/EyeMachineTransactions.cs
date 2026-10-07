using Extensions;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    internal static class EyeMachineTransactions
    {
        private static readonly AccessTools.FieldRef<BodyShreddingMachine, SFXComponent> BuySound = AccessTools.FieldRefAccess<BodyShreddingMachine, SFXComponent>("buyBackSfx");
        private static readonly System.Reflection.MethodInfo SellSound = AccessTools.Method(typeof(BodyShreddingMachine), "RpcOnEyeShredded");
        internal static bool Trade(BodyShreddingMachine machine, PlayerOrgans player, bool buying)
        {
            if (!NetworkServer.active || !player || player.connectionToClient == null) return false;
            var organs = NetworkSingleton<OrganManager>.Instance;
            var money = NetworkSingleton<MoneyManager>.Instance;
            if (!organs || !money || !organs.OrganData.TryGetValue(player.connectionToClient.connectionId, out var state) || state == null) return false;
            // Use canonical state, not a client-selected eye or a stale passed-in snapshot.
            bool randomRight = state.leftEye == state.rightEye && Random.value > .5f;
            bool success = EyeTradeRules.TryTransact(state.leftEye, state.rightEye, buying, randomRight, BodyEconomy.Get(MachinePart.Eye).Price(buying),
                delta => money.TryChangeTicketBalance(delta),
                (right, present) => organs.ServerToggleOrgan(player, right ? OrganType.RightEye : OrganType.LeftEye, present), out bool chosenRight);
            if (!success) return false;
            if (buying) BuySound(machine).RpcPlayOneShotWith3DPos(); else SellSound.Invoke(machine, null);
            CasinoChaosPlugin.Log($"Eye {(buying ? "buy" : "sell")}: player={player.netId}; side={(chosenRight ? "RightEye" : "LeftEye")}; tickets={BodyEconomy.Get(MachinePart.Eye).Price(buying)}; server=true");
            return true;
        }
    }
    [HarmonyPatch(typeof(BodyShreddingMachine), "TryShredEye")]
    internal static class SellLastEye
    { private static bool Prefix(BodyShreddingMachine __instance, PlayerOrgans po, ref bool __result) { __result = EyeMachineTransactions.Trade(__instance, po, false); return false; } }
    [HarmonyPatch(typeof(BodyShreddingMachine), "TryBuyEyeBack")]
    internal static class BuyRandomEye
    { private static bool Prefix(BodyShreddingMachine __instance, PlayerOrgans po, ref bool __result) { __result = EyeMachineTransactions.Trade(__instance, po, true); return false; } }
}
