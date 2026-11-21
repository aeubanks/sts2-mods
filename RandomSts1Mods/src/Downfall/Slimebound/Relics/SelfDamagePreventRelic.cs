using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Slimebound.Relics;

public sealed class SelfDamagePreventRelic : SlimeRelic, ITackleSelfDamageModifier
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Reduction", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.TackleHoverTip
    ];

    public int ModifyTackleSelfDamage(int currentDamage) =>
        Math.Max(0, currentDamage - DynamicVars["Reduction"].IntValue);
}
