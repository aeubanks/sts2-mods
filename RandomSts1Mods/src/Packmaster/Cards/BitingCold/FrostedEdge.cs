using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Packmaster.Powers;

namespace Packmaster.Cards.BitingCold;

public sealed class FrostedEdge()
    : PackmasterCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "frosted_edge";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FrostedEdgePower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<FrostbitePower>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrostedEdgePower>(choiceContext, Owner.Creature, DynamicVars["FrostedEdgePower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FrostedEdgePower"].UpgradeValueBy(1m);
    }
}
