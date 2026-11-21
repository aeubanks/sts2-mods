using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace Packmaster.Powers;

public sealed class EnigmancyPower : PackmasterPower
{
    protected override string IconKey => "enigmancy_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature != Owner || !Owner.IsAlive || Amount <= 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        int energy = Amount;
        Flash();
        await PlayerCmd.GainEnergy(energy, player);
        await PowerCmd.Decrement(this);
    }
}
