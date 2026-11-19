using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Downfall.Hermit.Powers;

public sealed class HermitDrawLessNextTurnPower : HermitPower
{
    protected override string IconKey => "draw_less_next_turn_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player)
            return count;

        return count - Amount;
    }

    public override async Task AfterModifyingHandDraw()
    {
        Flash();
        await PowerCmd.Remove(this);
    }
}
