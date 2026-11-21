using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Cards;

public sealed class HeroicBravado()
    : HermitCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "heroic_bravado";

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("CostIncrease", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<RuggedPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<RuggedPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        EnergyCost.AddThisCombat(DynamicVars["CostIncrease"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CostIncrease"].UpgradeValueBy(-1m);
    }
}
