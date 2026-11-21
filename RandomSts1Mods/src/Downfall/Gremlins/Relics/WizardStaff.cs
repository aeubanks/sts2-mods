using Downfall.Gremlins.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Gremlins.Relics;

public sealed class WizardStaff : GremlinRelic, IGremlinWizBonusProvider
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public decimal BonusWizDamage => DynamicVars["WizBonus"].BaseValue;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("WizBonus", 7m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WizPower>()
    ];
}
