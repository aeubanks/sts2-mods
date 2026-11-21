using MegaCrit.Sts2.Core.Combat;
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
/// Ship It (Rare, 1 cost):
/// Deal 5 (7) damage. Repeat for every Status card in your Exhaust Pile.
/// </summary>
public sealed class ShipIt() : BronzeCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move)
    ];

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        bool inCombat = IsMutable && CombatState is not null && Owner?.PlayerCombatState is not null;
        int statusCount = inCombat
            ? Owner!.PlayerCombatState!.ExhaustPile.Cards.Count(c => c.Type == CardType.Status)
            : 0;
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

        int totalHits = 1 + pcs.ExhaustPile.Cards.Count(c => c.Type == CardType.Status);
        await DealDamageAsync(
            choiceContext,
            owner,
            target,
            cardPlay,
            sourceCard,
            isFromPower,
            DynamicVars.Damage.BaseValue,
            hitCount: totalHits);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
