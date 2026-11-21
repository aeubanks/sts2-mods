using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Cards.Quiet;

public sealed class Weight()
    : PackmasterCard(-1, CardType.Status, CardRarity.Token, TargetType.None)
{
    protected override string PortraitKey => "weight";
    public override int MaxUpgradeLevel => 0;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Unplayable,
        CardKeyword.Retain
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    public override async Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (ReferenceEquals(card, this) && Pile?.Type == PileType.Discard)
        {
            await CardCmd.Exhaust(choiceContext, this);
        }
    }
}
