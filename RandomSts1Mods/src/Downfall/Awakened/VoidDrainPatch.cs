using HarmonyLib;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using VoidCard = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace Downfall.Awakened;

// Chains IEnergyDrainListener notifications strictly AFTER Void.AfterCardDrawn finishes LoseEnergy(1),
// so VoidRefundPower at 0 Energy refunds to 1 instead of being eaten by Void's energy loss.
[HarmonyPatch(typeof(VoidCard), nameof(VoidCard.AfterCardDrawn))]
internal static class VoidDrainPatch
{
    private static void Postfix(
        VoidCard __instance,
        PlayerChoiceContext choiceContext,
        CardModel card,
        ref Task __result)
    {
        if (!ReferenceEquals(card, __instance))
            return;

        __result = ChainDrainAsync(__result, __instance, choiceContext);
    }

    private static async Task ChainDrainAsync(Task voidTask, VoidCard voidCard, PlayerChoiceContext choiceContext)
    {
        await voidTask;
        int amount = voidCard.DynamicVars.Energy.IntValue;
        await AwakenedSpellbook.NotifyEnergyDrainedAsync(voidCard.Owner, voidCard.CombatState, choiceContext, amount);
    }
}
