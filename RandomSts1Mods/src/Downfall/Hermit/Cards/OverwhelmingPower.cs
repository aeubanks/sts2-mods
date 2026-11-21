using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Cards;

public sealed class OverwhelmingPower()
    : HermitCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "overwhelming_power";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(3),
        new CardsVar(3),
        new PowerVar<OverwhelmingPowerPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<OverwhelmingPowerPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(OverwhelmingPowerPower)].BaseValue,
            Owner.Creature,
            this);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
        DynamicVars[nameof(OverwhelmingPowerPower)].UpgradeValueBy(-1m);
    }
}
