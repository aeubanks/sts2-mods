using Downfall.Champ.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Champ;

// Prevents VigorPower from tracking/consuming Vigor when CrookedStrike attacks,
// while still letting VigorPower.ModifyDamageAdditive boost CrookedStrike's damage.
[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.BeforeAttack))]
internal static class CrookedStrikeVigorPatch
{
    private static bool Prefix(AttackCommand command, ref Task __result)
    {
        if (command.ModelSource is CrookedStrike)
        {
            __result = Task.CompletedTask;
            return false;
        }

        return true;
    }
}
