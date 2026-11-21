using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Packmaster.Powers;

public sealed class SlowingPower : PackmasterPower
{
    protected override string IconKey => "slowing_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature == Owner && Owner.IsAlive)
        {
            Flash();
            await PlayerCmd.GainEnergy(Amount, player);
        }
    }
}
