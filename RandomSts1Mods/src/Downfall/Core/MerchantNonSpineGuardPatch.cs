using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace Downfall.Core;

// Work around BaseLib 3.4.8 NodeFactory.TryAutoConvert not marking auto-converted scenes as CreatedFromFactory,
// which causes BaseLib's MerchantCharacterAnimPatch to miss non-Spine NMerchantCharacter scenes.
[HarmonyPatch(typeof(NMerchantCharacter))]
internal static class MerchantNonSpineGuardPatch
{
    private static bool ShouldRunSpine(NMerchantCharacter instance) =>
        (instance.GetChildCount() > 0 && instance.GetChild(0).GetClass() == "SpineSprite")
        || CustomAnimation.HasCustomAnimation(instance);

    [HarmonyPatch(nameof(NMerchantCharacter._Ready))]
    [HarmonyPrefix]
    private static bool ReadyPrefix(NMerchantCharacter __instance) => ShouldRunSpine(__instance);

    [HarmonyPatch(nameof(NMerchantCharacter.PlayAnimation))]
    [HarmonyPrefix]
    private static bool PlayAnimationPrefix(NMerchantCharacter __instance) => ShouldRunSpine(__instance);
}
