using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Champ.Powers;

public sealed class DefensiveStancePower : ChampStancePower
{
    public override ChampStanceKind StanceKind => ChampStanceKind.Defensive;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<CounterPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];
}
