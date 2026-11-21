using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Powers;

public sealed class MaintenanceStrikePower : HermitPower
{
    protected override string IconKey => "maintenance_strike_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer != Owner
            || !props.IsPoweredAttack()
            || cardSource is null
            || !cardSource.Tags.Contains(CardTag.Strike))
        {
            return 0m;
        }

        return Amount;
    }
}
