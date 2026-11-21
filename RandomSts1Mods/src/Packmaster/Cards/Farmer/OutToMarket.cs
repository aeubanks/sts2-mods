using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Farmer;

public sealed class OutToMarket()
    : PackmasterCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "out_to_market";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<OutToMarketPower>(1m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<OutToMarketPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(OutToMarketPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(OutToMarketPower)].UpgradeValueBy(1m);
    }
}
