using Downfall.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Powers;

/// <summary>
/// Thimble Helm (MoreBlockWithTempHPPower): While you have Temp HP, cards grant Amount additional Block.
/// </summary>
public sealed class MoreBlockWithTempHPPower : CollectorPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.TempHpHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target == Owner
            && cardSource is not null
            && props.IsPoweredCardOrMonsterMoveBlock()
            && Amount > 0
            && Owner.GetPowerAmount<DownfallTempHpPower>() > 0)
        {
            return Amount;
        }
        return 0m;
    }
}
