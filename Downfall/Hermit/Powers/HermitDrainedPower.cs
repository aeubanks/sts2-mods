using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hermit.Powers;

public sealed class HermitDrainedPower : HermitPower
{
    protected override string IconKey => "hermit_drained_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    public override async Task AfterEnergyReset(Player player)
    {
        if (player == Owner.Player && Amount > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            Flash();
            await PlayerCmd.LoseEnergy(Amount, player);
            await PowerCmd.Remove(this);
        }
    }
}
