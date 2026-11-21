using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Packmaster.Powers;

public sealed class DrawLessNextTurnPower : PackmasterPower
{
    protected override string IconKey => "draw_less_next_turn_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player || AmountOnTurnStart == 0)
            return count;

        return count - Amount;
    }

    public override async Task AfterModifyingHandDraw()
    {
        if (AmountOnTurnStart > 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
