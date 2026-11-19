using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Cards;

public sealed class Brawl()
    : HermitCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "brawl";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<BrawlPower>(3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BruisePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<BrawlPower>(choiceContext, Owner.Creature, DynamicVars[nameof(BrawlPower)].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(BrawlPower)].UpgradeValueBy(2m);
    }
}
