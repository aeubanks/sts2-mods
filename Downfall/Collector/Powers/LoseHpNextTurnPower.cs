using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Powers;

/// <summary>
/// Deathmarked (LoseHpNextTurnPower): At the end of its turn, loses Amount HP and removes itself.
/// </summary>
public sealed class LoseHpNextTurnPower : CollectorPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side
            || !participants.Contains(Owner)
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int loss = Amount;
        var owner = Owner;
        Flash();
        await PowerCmd.Remove(this);

        if (owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CreatureCmd.Damage(
                choiceContext,
                owner,
                loss,
                ValueProp.Unblockable | ValueProp.Unpowered,
                null,
                null);
        }
    }
}
