using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Quiet;

public sealed class HammerThrow()
    : PackmasterCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "hammer_throw";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<HammerThrowPower>(3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<HammerThrowPower>(choiceContext, Owner.Creature, DynamicVars["HammerThrowPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HammerThrowPower"].UpgradeValueBy(2m);
    }
}
