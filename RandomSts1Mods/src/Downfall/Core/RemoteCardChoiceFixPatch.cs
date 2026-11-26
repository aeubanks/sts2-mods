using System.Collections;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace Downfall.Core;

/// <summary>
/// Fixes multiplayer scene-tree reparenting errors when a remote player plays a Power card that
/// triggers a player choice (such as Automaton's Mutator).
///
/// CardModel.OnPlayWrapper starts NCardFlyPowerVfx on the card node and only waits 20% of its
/// duration before invoking OnPlay. When OnPlay pauses for a player choice,
/// NMultiplayerPlayerIntentHandler.BeforeActionPausedForPlayerChoice finds the still-flying NCard
/// via NCard.FindOnTable and stores it as _cardInPlayAwaitingPlayerChoice. Meanwhile,
/// NCardFlyPowerVfx finishes and frees/detaches that NCard. When the remote choice later finishes,
/// NCardPlayQueue.ReAddCardAfterPlayerChoice and BeforeRemoteCardPlayResumedAfterPlayerChoice try
/// to Reparent/MoveChildSafely on the parentless/freed node.
/// </summary>
internal static class RemoteCardChoiceFixPatch
{
    [ThreadStatic]
    private static bool _inRemoteChoicePause;

    private static bool IsCardOwnedByPowerFlyVfx(NCard card)
    {
        if (!GodotObject.IsInstanceValid(card) || !card.IsInsideTree() || card.GetParent() == null)
            return true;

        if (card.Model?.Type == CardType.Power)
            return true;

        var vfxContainer = NCombatRoom.Instance?.CombatVfxContainer;
        if (vfxContainer != null)
        {
            foreach (var child in vfxContainer.GetChildren())
            {
                if (child is NCardFlyPowerVfx flyVfx && flyVfx.CardNode == card)
                    return true;
            }
        }

        return false;
    }

    private static bool IsNodeValidAndParented(Node? node) =>
        node != null
        && GodotObject.IsInstanceValid(node)
        && node.IsInsideTree()
        && node.GetParent() != null;

    [HarmonyPatch(typeof(NMultiplayerPlayerIntentHandler), "BeforeActionPausedForPlayerChoice")]
    internal static class IntentHandlerPauseScopePatch
    {
        [HarmonyPrefix]
        private static void Prefix() => _inRemoteChoicePause = true;

        [HarmonyFinalizer]
        private static void Finalizer() => _inRemoteChoicePause = false;
    }

    [HarmonyPatch(typeof(NCard), nameof(NCard.FindOnTable))]
    internal static class FindOnTablePowerFlyGuardPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref NCard? __result)
        {
            if (_inRemoteChoicePause && __result != null && IsCardOwnedByPowerFlyVfx(__result))
            {
                __result = null;
            }
        }
    }

    [HarmonyPatch(typeof(NCardPlayQueue), nameof(NCardPlayQueue.ReAddCardAfterPlayerChoice))]
    internal static class ReAddCardAfterPlayerChoiceGuardPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(NCard card)
        {
            return IsNodeValidAndParented(card);
        }
    }

    [HarmonyPatch(typeof(NCardPlayQueue), "BeforeRemoteCardPlayResumedAfterPlayerChoice")]
    internal static class BeforeRemoteResumeGuardPatch
    {
        private static readonly FieldInfo? PlayQueueField = AccessTools.Field(typeof(NCardPlayQueue), "_playQueue");
        private static readonly MethodInfo? RemoveCardFromQueueMethod = AccessTools.Method(typeof(NCardPlayQueue), "RemoveCardFromQueue", [typeof(int)]);
        private static readonly MethodInfo? ResumeHandlerMethod = AccessTools.Method(typeof(NCardPlayQueue), "BeforeRemoteCardPlayResumedAfterPlayerChoice", [typeof(GameAction)]);

        [HarmonyPrefix]
        private static bool Prefix(NCardPlayQueue __instance, GameAction action)
        {
            if (PlayQueueField?.GetValue(__instance) is not IList queue)
                return true;

            for (int i = 0; i < queue.Count; i++)
            {
                var item = queue[i];
                if (item == null)
                    continue;

                var itemType = item.GetType();
                var itemAction = itemType.GetField("action")?.GetValue(item) as GameAction;
                if (!ReferenceEquals(itemAction, action))
                    continue;

                var itemCard = itemType.GetField("card")?.GetValue(item) as NCard;
                if (!IsNodeValidAndParented(itemCard))
                {
                    if (ResumeHandlerMethod != null)
                    {
                        var del = (Action<GameAction>)Delegate.CreateDelegate(typeof(Action<GameAction>), __instance, ResumeHandlerMethod);
                        action.BeforeResumedAfterPlayerChoice -= del;
                    }
                    RemoveCardFromQueueMethod?.Invoke(__instance, [i]);
                    return false;
                }

                break;
            }

            return true;
        }
    }
}
