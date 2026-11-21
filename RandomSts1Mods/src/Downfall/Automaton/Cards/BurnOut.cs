using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Burn Out (Rare, 1 cost, Exhaust):
/// Deal 6 (9) damage to a random enemy for ALL of your Status cards (Draw, Discard, Hand), then Exhaust them.
/// </summary>
public sealed class BurnOut() : BronzeCard(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move)
    ];

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        bool inCombat = IsMutable && CombatState is not null && Owner?.PlayerCombatState is not null;
        int statusCount = inCombat ? CountCombatStatuses(Owner!) : 0;
        description.Add("InCombat", inCombat);
        description.Add("StatusCount", statusCount);
    }

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.PlayerCombatState is not { } pcs
            || !owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var statuses = new List<CardModel>();
        statuses.AddRange(pcs.DrawPile.Cards.Where(c => c.Type == CardType.Status));
        statuses.AddRange(pcs.DiscardPile.Cards.Where(c => c.Type == CardType.Status));
        statuses.AddRange(pcs.Hand.Cards.Where(c => c.Type == CardType.Status));

        int statusCount = statuses.Count;
        foreach (var status in statuses)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;
            if (status.Pile?.Type != PileType.Exhaust)
                await CardCmd.Exhaust(choiceContext, status);
        }

        for (int i = 0; i < statusCount; i++)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await DealDamageAsync(
                choiceContext,
                owner,
                target: null,
                cardPlay,
                sourceCard,
                isFromPower,
                DynamicVars.Damage.BaseValue,
                hitCount: 1);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }

    private static int CountCombatStatuses(Player player)
    {
        if (player.PlayerCombatState is not { } pcs)
            return 0;

        return pcs.DrawPile.Cards.Count(c => c.Type == CardType.Status)
            + pcs.DiscardPile.Cards.Count(c => c.Type == CardType.Status)
            + pcs.Hand.Cards.Count(c => c.Type == CardType.Status);
    }
}
