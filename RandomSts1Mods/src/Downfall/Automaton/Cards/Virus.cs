using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Virus (Rare, 1 cost, Exhaust):
/// Deal 4 (6) damage. Discard your Hand and add a Minor Beam (Minor Beam+) to your Hand for each card discarded.
/// </summary>
public sealed class Virus() : BronzeCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<MinorBeam>(upgrade: IsUpgraded)
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.PlayerCombatState is not { } pcs
            || owner.Creature.CombatState is not { } combatState
            || !owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);

        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var toDiscard = pcs.Hand.Cards.Where(c => !ReferenceEquals(c, this)).ToList();
        int discardCount = toDiscard.Count;
        if (discardCount == 0)
            return;

        await CardCmd.Discard(choiceContext, toDiscard);

        for (int i = 0; i < discardCount; i++)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var beam = combatState.CreateCard<MinorBeam>(owner);
            if (IsUpgraded)
                CardCmd.Upgrade(beam);
            await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(beam, owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
