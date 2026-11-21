using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.EnergyAndEcho;

public sealed class Panacher()
    : PackmasterCard(0, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "panacher";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<PanacherPower>(10m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<PanacherPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(PanacherPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(PanacherPower)].UpgradeValueBy(4m);
    }
}
