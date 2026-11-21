using BaseLib.Cards.Variables;
using BaseLib.Commands;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Warden;

public sealed class Enigmancy()
    : PackmasterCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "enigmancy";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ScryVar(5m),
        new PowerVar<EnigmancyPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ScryCmd.Execute(choiceContext, this);
        await PowerCmd.Apply<EnigmancyPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(EnigmancyPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Scry"].UpgradeValueBy(1m);
        DynamicVars[nameof(EnigmancyPower)].UpgradeValueBy(1m);
    }
}
