using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.HighEnergy;

public sealed class Investor()
    : PackmasterCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "investor";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<InvestorPower>(6m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<InvestorPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["InvestorPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["InvestorPower"].UpgradeValueBy(3m);
    }
}
