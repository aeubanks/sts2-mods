using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace Downfall.Collector.Relics;

/// <summary>
/// Fuel Canister (Shop):
/// At the end of your turn, Retain the lowest-cost card in your hand (excluding Ethereal and already-Retained cards).
/// </summary>
public sealed class FuelCanister : CollectorRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    protected override bool IsAllowedInternal(IRunState runState) =>
        !runState.Players.Any(p => p.Relics.Any(r => r.Id.Entry == "RUNIC_PYRAMID"));

    public override Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner?.PlayerCombatState is not { } pcs
            || side != CombatSide.Player
            || !participants.Contains(Owner.Creature)
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return Task.CompletedTask;
        }

        var eligible = pcs.Hand.Cards
            .Where(c => !c.Keywords.Contains(CardKeyword.Ethereal)
                        && !c.Keywords.Contains(CardKeyword.Retain)
                        && !c.ShouldRetainThisTurn)
            .ToList();

        if (eligible.Count == 0)
            return Task.CompletedTask;

        // Prefer non-X cards with cost >= 0 (lowest cost first); fall back to Unplayable/negative-cost cards.
        var nonNegative = eligible
            .Where(c => !c.EnergyCost.CostsX && c.EnergyCost.GetWithModifiers(CostModifiers.All) >= 0)
            .ToList();

        List<CardModel> candidates;
        if (nonNegative.Count > 0)
        {
            int minCost = nonNegative.Min(c => c.EnergyCost.GetWithModifiers(CostModifiers.All));
            candidates = nonNegative.Where(c => c.EnergyCost.GetWithModifiers(CostModifiers.All) == minCost).ToList();
        }
        else
        {
            var nonX = eligible.Where(c => !c.EnergyCost.CostsX).ToList();
            if (nonX.Count == 0)
                return Task.CompletedTask;
            candidates = nonX;
        }

        var chosen = Owner.RunState.Rng.CombatCardGeneration.NextItem(candidates);
        if (chosen is not null)
        {
            chosen.GiveSingleTurnRetain();
            Flash();
        }

        return Task.CompletedTask;
    }
}
