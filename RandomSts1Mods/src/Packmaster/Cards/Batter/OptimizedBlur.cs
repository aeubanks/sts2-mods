using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Batter;

public sealed class OptimizedBlur()
    : PackmasterCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "optimized_blur";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<OptimizedBlurPower>(2m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<OptimizedBlurPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["OptimizedBlurPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["OptimizedBlurPower"].UpgradeValueBy(1m);
    }
}
