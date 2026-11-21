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
/// HYPER BEAM (HyperBeam, Rare, 1 cost, Retain):
/// Deal 25 (35) damage to ALL enemies. Put 5 Voids on top of your Draw Pile.
/// </summary>
public sealed class HyperBeam() : BronzeCard(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(25m, ValueProp.Move),
        new CardsVar("Voids", 5)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<MegaCrit.Sts2.Core.Models.Cards.Void>()
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

        int voidCount = DynamicVars["Voids"].IntValue;
        for (int i = 0; i < voidCount; i++)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            var voidCard = combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(owner);
            await CardPileCmd.AddGeneratedCardToCombat(voidCard, PileType.Draw, owner, CardPilePosition.Top);
        }

        await DealAllEnemiesDamageAsync(choiceContext, owner, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(10m);
    }
}
