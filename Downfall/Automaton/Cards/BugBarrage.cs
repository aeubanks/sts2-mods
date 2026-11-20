using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Bug Barrage (Common, 1 cost):
/// Add 2 Wounds to your Hand. Cycle each Status card in your Hand, dealing 5 (7) damage for each.
/// </summary>
public sealed class BugBarrage() : BronzeCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new IntVar("Wounds", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CycleHoverTip,
        HoverTipFactory.FromCard<Wound>()
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.Creature.CombatState is not { } combatState
            || !owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int woundCount = DynamicVars["Wounds"].IntValue;
        var hand = PileType.Hand.GetPile(owner);
        for (int i = 0; i < woundCount; i++)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;
            var wound = combatState.CreateCard<Wound>(owner);
            await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(wound, owner);
        }

        var statusesInHand = hand.Cards.Where(c => c.Type == CardType.Status).ToList();
        int cycled = 0;
        foreach (var status in statusesInHand)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            if (status.Pile?.Type == PileType.Hand)
            {
                await CardCmd.Discard(choiceContext, status);
                cycled++;
                if (target is { IsAlive: true })
                    await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
            }
        }

        if (cycled > 0 && owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, cycled, owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
