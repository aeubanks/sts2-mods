using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Shaman;

public sealed class Pyromastery()
    : PackmasterCard(0, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "pyromastery";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<PyromasteryPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<IgnitePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<PyromasteryPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(PyromasteryPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(PyromasteryPower)].UpgradeValueBy(1m);
    }
}
