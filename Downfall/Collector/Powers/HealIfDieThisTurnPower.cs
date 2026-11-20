using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Powers;

/// <summary>
/// Finalized (HealIfDieThisTurnPower): When this enemy dies, heal the Collector for Amount HP.
/// </summary>
public sealed class HealIfDieThisTurnPower : CollectorPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        CardModel? source,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        if (creature != Owner || wasRemovalPrevented || Amount <= 0)
            return;

        var healer = Applier is { IsAlive: true }
            ? Applier
            : CombatState?.Players.FirstOrDefault(p => p.Creature.IsAlive)?.Creature;
        if (healer is null)
            return;

        Flash();
        await CreatureCmd.Heal(healer, Amount);
    }
}
