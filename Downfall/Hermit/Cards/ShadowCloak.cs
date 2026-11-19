using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Cards;

public sealed class ShadowCloak()
    : HermitCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "shadow_cloak";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ShadowCloakPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ShadowCloakPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(ShadowCloakPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(ShadowCloakPower)].UpgradeValueBy(2m);
    }
}
