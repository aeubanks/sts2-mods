using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Powers;

public sealed class AwakenDeathPower : AwakenedPower
{
    private bool _triggered;
    private bool _immuneRestOfTurn;

    protected override string IconKey => "awaken_death_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggered = false;
        _immuneRestOfTurn = false;
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (creature == Owner && !_triggered && Amount > 0)
            return false;

        return true;
    }

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner && _immuneRestOfTurn)
            return 0m;

        return amount;
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner || _triggered || Amount <= 0)
            return;

        _triggered = true;
        Flash();

        foreach (var debuff in Owner.Powers.Where(p => p.Type == PowerType.Debuff).ToList())
        {
            await PowerCmd.Remove(debuff);
        }

        await CreatureCmd.Heal(Owner, Amount);

        if (CombatState is not null && Owner.Player is not null)
        {
            await AwakenedSpellbook.TryCheckAwakeningAsync(
                CombatState,
                Owner.Player,
                new ThrowingPlayerChoiceContext(),
                force: true);
        }

        if (CombatState is not null && CombatState.CurrentSide != Owner.Side)
        {
            _immuneRestOfTurn = true;
        }
        else
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (_immuneRestOfTurn && side != Owner.Side && !CombatManager.Instance.IsOverOrEnding)
        {
            _immuneRestOfTurn = false;
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (_triggered
            || !Owner.IsAlive
            || Amount <= 0
            || (CombatState is not null && CombatState.HittableEnemies.Count > 0))
        {
            return;
        }

        _triggered = true;
        Flash();
        await CreatureCmd.Heal(Owner, Amount);
    }
}
