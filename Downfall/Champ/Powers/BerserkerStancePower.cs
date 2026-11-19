using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Champ.Powers;

public sealed class BerserkerStancePower : ChampStancePower
{
    public override ChampStanceKind StanceKind => ChampStanceKind.Berserker;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VigorPower>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];
}
