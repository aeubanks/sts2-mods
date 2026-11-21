using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Shaman;

public sealed class FueledByEmbers()
    : PackmasterCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "fueled_by_embers";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FueledByEmbersPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<FadingEmber>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FueledByEmbersPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(FueledByEmbersPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
