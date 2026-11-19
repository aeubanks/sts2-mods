using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class Spite()
    : HermitCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "spite";

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new CardsVar(3)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromKeyword(CardKeyword.Unplayable)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var unplayable = PileType.Hand.GetPile(Owner).Cards
            .Where(c => !ReferenceEquals(c, this) && c.Keywords.Contains(CardKeyword.Unplayable))
            .ToList();

        foreach (var card in unplayable)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
