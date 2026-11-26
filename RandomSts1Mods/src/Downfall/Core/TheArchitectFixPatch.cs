using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Models.Events;

namespace Downfall.Core;

/// <summary>
/// Prevents NullReferenceException in TheArchitect.WinRun() when Dialogue is null
/// (e.g., for any modded character without matching Architect dialogues or after exhausting non-repeating dialogues).
/// </summary>
[HarmonyPatch(typeof(TheArchitect), "LoadDialogue")]
internal static class TheArchitectFixPatch
{
    private static readonly PropertyInfo? DialogueProp = AccessTools.Property(typeof(TheArchitect), "Dialogue");

    [HarmonyPostfix]
    private static void EnsureDialogueNonNull(TheArchitect __instance)
    {
        if (DialogueProp == null)
            return;

        if (DialogueProp.GetValue(__instance) == null)
        {
            DialogueProp.SetValue(__instance, new AncientDialogue
            {
                EndAttackers = ArchitectAttackers.Both
            });
        }
    }
}
