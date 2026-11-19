using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Powers;

public sealed class RuggedPower : HermitPower
{
    protected override string IconKey => "rugged_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (!CombatManager.Instance.IsInProgress
            || target != Owner
            || dealer is null
            || !props.IsPoweredAttack()
            || amount <= 2m)
        {
            return amount;
        }

        return 2m;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        Flash();
        await PowerCmd.Decrement(this);
    }
}
