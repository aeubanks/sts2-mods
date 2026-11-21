using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Powers;

public sealed class BuffSecondarySlimeEffectsPower : SlimePower, IMinionMasterProvider
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int ExtraCommands => Math.Max(0, Amount);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.CommandHoverTip
    ];
}
