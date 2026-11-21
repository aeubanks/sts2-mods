using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Bramblespar Kindling (Common, Unplayable, Skill):
/// When Exhausted, add a Burning Strike (Burning Strike+) into your hand.
/// </summary>
public sealed class BramblesparKindling() : CollectorCard(-1, CardType.Skill, CardRarity.Common, TargetType.None)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<BurningStrike>(IsUpgraded)
    ];

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card != this || Owner is null || CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var strike = CombatState.CreateCard<BurningStrike>(Owner);
        if (IsUpgraded)
            strike.UpgradeInternal();
        await CardPileCmd.AddGeneratedCardToCombat(strike, PileType.Hand, Owner);
    }
}
