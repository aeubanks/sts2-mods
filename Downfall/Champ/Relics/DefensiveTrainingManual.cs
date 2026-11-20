using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Relics;

public sealed class DefensiveTrainingManual : ChampRelic
{
    public const decimal BonusBlock = 3m;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(BonusBlock, ValueProp.Unpowered)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ChampCard.DefensiveHoverTip,
        ChampCard.FinisherHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    internal void FlashRelic() => Flash();
}
