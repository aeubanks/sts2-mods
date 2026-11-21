using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace Downfall.Snecko;

internal interface ISneckoMuddleCard
{
    void OnMuddled();
}

internal interface ISneckoMuddleListener
{
    Task OnCardMuddledAsync(PlayerChoiceContext choiceContext, CardModel card);
}

internal interface ISneckoOverflowListener
{
    Task OnOverflowTriggeredAsync(PlayerChoiceContext choiceContext, SneckoCard source);
}

internal interface ISneckoOverflowOverride;

internal static class SneckoMechanics
{
    internal static LocString MuddleSelectionPrompt =>
        new("gameplay_ui", "DOWNFALL-SNECKO_MUDDLE.selectionScreenPrompt");

    internal static bool CanMuddle(CardModel card) =>
        !card.EnergyCost.CostsX && card.EnergyCost.Canonical >= 0;

    internal static bool CanSelectForMuddle(CardModel card) =>
        CanMuddle(card) || card is ISneckoMuddleCard;

    internal static bool EvaluateOverflow(Player? owner, int handCountOffset = 0)
    {
        if (owner?.PlayerCombatState is null)
            return false;

        if (owner.Creature.Powers.Any(p => p is ISneckoOverflowOverride))
            return true;

        return PileType.Hand.GetPile(owner).Cards.Count + handCountOffset > 5;
    }

    internal static Task<bool> MuddleAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel card,
        bool noThree = false) =>
        MuddleCoreAsync(choiceContext, player, card, cheaper: false, noThree: noThree);

    internal static Task<bool> MuddleCheaperAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel card,
        bool noThree = false) =>
        MuddleCoreAsync(choiceContext, player, card, cheaper: true, noThree: noThree);

    private static async Task<bool> MuddleCoreAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel card,
        bool cheaper,
        bool noThree)
    {
        if (card is ISneckoMuddleCard muddleCard)
        {
            muddleCard.OnMuddled();
        }

        if (!CanMuddle(card))
            return false;

        int currentCost = card.EnergyCost.GetWithModifiers(CostModifiers.All);
        int maxCost = noThree ? 2 : 3;
        var options = new List<int>(4);
        if (cheaper)
        {
            int maxCandidate = Math.Min(maxCost, currentCost - 1);
            for (int c = 0; c <= maxCandidate; c++)
            {
                options.Add(c);
            }
            if (options.Count == 0)
            {
                options.Add(0);
            }
        }
        else
        {
            for (int c = 0; c <= maxCost; c++)
            {
                if (c != currentCost)
                    options.Add(c);
            }
        }

        if (options.Count == 0)
            return false;

        int newCost = options[player.RunState.Rng.CombatEnergyCosts.NextInt(options.Count)];
        card.EnergyCost.SetThisTurn(newCost);
        NCard.FindOnTable(card)?.PlayRandomizeCostAnim();

        await NotifyMuddleListenersAsync(choiceContext, player, card);
        return true;
    }

    internal static async Task SelectAndMuddleFromHandAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int count,
        AbstractModel source,
        bool cheaper = false)
    {
        if (count <= 0 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var hand = PileType.Hand.GetPile(player).Cards;
        int eligibleCount = hand.Count(CanSelectForMuddle);
        if (eligibleCount == 0)
            return;

        int pickCount = Math.Min(count, eligibleCount);
        var prefs = new CardSelectorPrefs(MuddleSelectionPrompt, pickCount);
        var selected = (await CardSelectCmd.FromHand(choiceContext, player, prefs, CanSelectForMuddle, source)).ToList();
        foreach (var card in selected)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            if (card.Pile?.Type != PileType.Hand)
                continue;

            await MuddleCoreAsync(choiceContext, player, card, cheaper: cheaper, noThree: false);
        }
    }

    internal static async Task MuddleHandAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        bool noThree = false)
    {
        foreach (var card in PileType.Hand.GetPile(player).Cards.ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            if (card.Pile?.Type != PileType.Hand)
                continue;

            await MuddleCoreAsync(choiceContext, player, card, cheaper: false, noThree: noThree);
        }
    }

    internal static async Task MuddleHighestCostInHandAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int count,
        bool noThree = false)
    {
        if (count <= 0 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var candidates = PileType.Hand.GetPile(player).Cards
            .Where(CanMuddle)
            .ToList();

        var rng = player.RunState.Rng.CombatCardGeneration;
        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            candidates.RemoveAll(c => c.Pile?.Type != PileType.Hand);
            if (candidates.Count == 0)
                break;

            int highest = candidates.Max(c => c.EnergyCost.GetWithModifiers(CostModifiers.All));
            var tied = candidates.Where(c => c.EnergyCost.GetWithModifiers(CostModifiers.All) == highest).ToList();
            var picked = tied[rng.NextInt(tied.Count)];
            candidates.Remove(picked);
            await MuddleCoreAsync(choiceContext, player, picked, cheaper: false, noThree: noThree);
        }
    }

    internal static async Task NotifyOverflowTriggeredAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        SneckoCard source)
    {
        if (source.IsTyphoonAutoPlay || CombatManager.Instance.IsOverOrEnding)
            return;

        foreach (var listener in player.Creature.Powers.OfType<ISneckoOverflowListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnOverflowTriggeredAsync(choiceContext, source);
        }

        foreach (var listener in player.Relics.OfType<ISneckoOverflowListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnOverflowTriggeredAsync(choiceContext, source);
        }
    }

    internal static bool IsHostileDebuffApplied(PowerModel power, decimal amount, Creature? applier)
    {
        if (applier is null || applier.Side == power.Owner.Side)
            return false;

        if (power is TemporaryStrengthPower)
            return false;

        return (power.Type == PowerType.Debuff && amount > 0m)
            || (power.Type == PowerType.Buff && power.AllowNegative && amount < 0m);
    }

    internal static int CountDebuffs(Creature target) =>
        target.Powers.Count(p => p.TypeForCurrentAmount == PowerType.Debuff);

    private static async Task NotifyMuddleListenersAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel card)
    {
        foreach (var listener in player.Creature.Powers.OfType<ISneckoMuddleListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnCardMuddledAsync(choiceContext, card);
        }

        foreach (var listener in player.Relics.OfType<ISneckoMuddleListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnCardMuddledAsync(choiceContext, card);
        }
    }
}
