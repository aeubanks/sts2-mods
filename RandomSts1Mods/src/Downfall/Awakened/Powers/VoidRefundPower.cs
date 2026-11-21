using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Powers;

public sealed class VoidRefundPower : AwakenedPower, IEnergyDrainListener
{
    protected override string IconKey => "void_refund_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    public async Task OnEnergyDrainedAsync(PlayerChoiceContext choiceContext, int energyLost)
    {
        if (Owner.Player is null || !Owner.IsAlive || Amount <= 0 || energyLost <= 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        int refund = Amount;
        Flash();
        await PowerCmd.Remove(this);

        if (CombatState?.CurrentSide == CombatSide.Player)
        {
            await PlayerCmd.GainEnergy(refund, Owner.Player);
        }
        else
        {
            await PowerCmd.Apply<EnergyNextTurnPower>(choiceContext, Owner, refund, Owner, null);
        }
    }
}
