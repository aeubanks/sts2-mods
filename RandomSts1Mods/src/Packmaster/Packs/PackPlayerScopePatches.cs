using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace Packmaster.Packs;

/// <summary>
/// Scopes PackRunState.CurrentScopedPlayer around player-specific card pool lookups so that
/// multiple Packmasters in multiplayer each pull rewards, shop stock, and transforms from
/// their own drafted Pack sets.
/// </summary>
internal static class PackPlayerScopePatches
{
    [HarmonyPatch(typeof(CardCreationOptions), nameof(CardCreationOptions.GetPossibleCards))]
    internal static class CardCreationOptionsScopePatch
    {
        // GetPossibleCards returns a lazy LINQ IEnumerable, so materialize it inside the player scope.
        [HarmonyPostfix]
        private static void Postfix(Player player, ref IEnumerable<CardModel> __result)
        {
            using (PackRunState.ScopePlayer(player))
            {
                __result = __result.ToList();
            }
        }
    }

    [HarmonyPatch(typeof(MerchantInventory), "GetDefaultCharacterCardEntries")]
    internal static class MerchantDefaultEntriesScopePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Player player, out IDisposable __state) =>
            __state = PackRunState.ScopePlayer(player);

        [HarmonyFinalizer]
        private static void Finalizer(IDisposable? __state) => __state?.Dispose();
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForMerchant), [typeof(Player), typeof(IEnumerable<CardModel>), typeof(IEnumerable<CardModel>), typeof(CardType)])]
    internal static class MerchantCardByTypeScopePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Player player, out IDisposable __state) =>
            __state = PackRunState.ScopePlayer(player);

        [HarmonyFinalizer]
        private static void Finalizer(IDisposable? __state) => __state?.Dispose();
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForMerchant), [typeof(Player), typeof(IEnumerable<CardModel>), typeof(IEnumerable<CardModel>), typeof(CardRarity)])]
    internal static class MerchantCardByRarityScopePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Player player, out IDisposable __state) =>
            __state = PackRunState.ScopePlayer(player);

        [HarmonyFinalizer]
        private static void Finalizer(IDisposable? __state) => __state?.Dispose();
    }

    [HarmonyPatch(typeof(CardFactory), nameof(CardFactory.GetDefaultTransformationOptions))]
    internal static class TransformOptionsScopePatch
    {
        [HarmonyPrefix]
        private static void Prefix(CardModel original, out IDisposable __state) =>
            __state = PackRunState.ScopePlayer(original?.Owner);

        [HarmonyPostfix]
        private static void Postfix(CardModel original, ref IEnumerable<CardModel> __result)
        {
            using (PackRunState.ScopePlayer(original?.Owner))
            {
                __result = __result.ToList();
            }
        }

        [HarmonyFinalizer]
        private static void Finalizer(IDisposable? __state) => __state?.Dispose();
    }
}
