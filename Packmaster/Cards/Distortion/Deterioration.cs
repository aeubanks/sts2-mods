using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Distortion;

public sealed class Deterioration()
    : PackmasterCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "deterioration";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<DeteriorationPower>(5m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<DeteriorationPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(DeteriorationPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(DeteriorationPower)].UpgradeValueBy(2m);
    }
}
