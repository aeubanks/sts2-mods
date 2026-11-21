using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class IgnitePower : PackmasterPower
{
    protected override string IconKey => "ignite_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!Owner.IsPlayer && participants.Contains(Owner))
        {
            await TriggerDamageAsync(new ThrowingPlayerChoiceContext());
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.IsPlayer && participants.Contains(Owner))
        {
            await TriggerDamageAsync(choiceContext);
        }
    }

    private async Task TriggerDamageAsync(PlayerChoiceContext choiceContext)
    {
        if (!Owner.IsAlive || Amount <= 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            Amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }
}
