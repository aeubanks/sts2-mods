using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Powers;

public sealed class CursedStrengthPower : AwakenedPower, IEnergyDrainListener
{
    protected override string IconKey => "cursed_strength_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnEnergyDrainedAsync(PlayerChoiceContext choiceContext, int energyLost)
    {
        if (energyLost <= 0 || Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount * energyLost, Owner, null);
    }
}
