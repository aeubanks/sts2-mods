using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Awakened.Relics;

public sealed class StrengthBooster : AwakenedRelic
{
    public const int BonusPercent = 25;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Percent", BonusPercent)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ManaburnPower>()
    ];

    internal void TriggerFlash() => Flash();
}
