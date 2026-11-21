using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Powers;

public sealed class DarknessFallsPower : AwakenedPower, IEnergyDrainListener
{
    protected override string IconKey => "darkness_falls_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnEnergyDrainedAsync(PlayerChoiceContext choiceContext, int energyLost)
    {
        if (energyLost <= 0 || Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner, Amount * energyLost, ValueProp.Unpowered, null);
    }
}
