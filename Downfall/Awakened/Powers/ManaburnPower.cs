using Downfall.Awakened.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Powers;

public sealed class ManaburnPower : AwakenedPower, IEnergyDrainListener
{
    protected override string IconKey => "manaburn_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnEnergyDrainedAsync(PlayerChoiceContext choiceContext, int energyLost)
    {
        if (!Owner.IsAlive || Amount <= 0 || energyLost <= 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        int damage = Amount * energyLost;
        var paperCrow = CombatState?.Players
            .SelectMany(p => p.Relics.OfType<StrengthBooster>())
            .FirstOrDefault();
        if (paperCrow is not null)
        {
            paperCrow.TriggerFlash();
            damage += damage / 4;
        }

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            damage,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }
}
