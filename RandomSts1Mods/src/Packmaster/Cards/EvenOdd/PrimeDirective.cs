using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.EvenOdd;

public sealed class PrimeDirective()
    : PackmasterCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "prime_directive";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<PrimeDirectivePower>(4m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<PrimeDirectivePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["PrimeDirectivePower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["PrimeDirectivePower"].UpgradeValueBy(1m);
    }
}
