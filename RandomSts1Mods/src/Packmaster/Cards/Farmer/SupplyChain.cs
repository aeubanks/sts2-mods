using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Farmer;

public sealed class SupplyChain()
    : PackmasterCard(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "supply_chain";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SupplyChainPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Fertilizer>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SupplyChainPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(SupplyChainPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
