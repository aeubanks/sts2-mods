using Downfall.Snecko.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Snecko.Cards;

public sealed class BlunderGuard() : SneckoCard(0, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<BigGunsBlockPower>(6m),
        new PowerVar<BigGunsStrengthPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<BigGunsBlockPower>(choiceContext, Owner.Creature, DynamicVars["BigGunsBlockPower"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<BigGunsStrengthPower>(choiceContext, Owner.Creature, DynamicVars["BigGunsStrengthPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BigGunsBlockPower"].UpgradeValueBy(2m);
        DynamicVars["BigGunsStrengthPower"].UpgradeValueBy(1m);
    }
}
