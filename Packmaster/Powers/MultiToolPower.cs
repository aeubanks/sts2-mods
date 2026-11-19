using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class MultiToolPower : PackmasterPower
{
    protected override string IconKey => "multi_tool_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
            return;

        if (Owner.IsAlive && Owner.Player?.PlayerCombatState?.Energy > 0 && Owner.CombatState is not null)
        {
            Flash();
            await CreatureCmd.Damage(
                choiceContext,
                Owner.CombatState.HittableEnemies,
                Amount,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: Owner,
                cardSource: null,
                cardPlay: null);
        }

        await PowerCmd.Remove(this);
    }
}
