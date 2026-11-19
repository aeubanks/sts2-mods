using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Quiet;

public sealed class IronShield()
    : PackmasterCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override string PortraitKey => "iron_shield";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(11m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Weight>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        var weight = CombatState!.CreateCard<Weight>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(weight, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
