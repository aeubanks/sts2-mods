using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Collector.Powers;

/// <summary>
/// Demise (DemisePower): Prevents Doom from being removed the next Amount time(s) it triggers
/// while the creature is not Afflicted (decrements by 1 each time).
/// </summary>
public sealed class CollectorDemisePower : CollectorPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.DoomHoverTip,
        CollectorCard.AfflictedHoverTip
    ];
}
