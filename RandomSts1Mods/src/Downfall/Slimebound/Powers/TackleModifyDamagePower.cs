using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Powers;

public sealed class TackleModifyDamagePower : SlimePower, ITackleSelfDamageModifier
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.TackleHoverTip
    ];

    public int ModifyTackleSelfDamage(int currentDamage) => currentDamage + Amount;
}
