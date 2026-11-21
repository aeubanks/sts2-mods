using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Haunted Hand (Haunt): Ethereal. Afterlife. Draw 1 (2) card(s), then draw cards until you draw a non-Ethereal card.
/// Afterlife: Draw cards until you draw a non-Ethereal card.
/// </summary>
public sealed class Haunt() : HexaCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];
    public override bool HasAfterlife => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AfterlifeHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        await DrawUntilNonEtherealAsync(choiceContext);
    }

    protected override async Task OnAfterlifeAsync(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        await DrawUntilNonEtherealAsync(choiceContext);
    }

    private async Task DrawUntilNonEtherealAsync(PlayerChoiceContext choiceContext)
    {
        for (int i = 0; i < CardPile.MaxCardsInHand; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var drawn = (await CardPileCmd.Draw(choiceContext, 1m, Owner)).FirstOrDefault();
            if (drawn is null || !drawn.Keywords.Contains(CardKeyword.Ethereal))
                break;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
