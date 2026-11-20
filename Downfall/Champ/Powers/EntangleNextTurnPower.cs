using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Champ.Powers;

public sealed class EntangleNextTurnPower : ChampPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int turns = Amount;
        Flash();
        await PowerCmd.Remove(this);
        if (turns > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<ChampEntanglePower>(new ThrowingPlayerChoiceContext(), Owner, turns, Owner, null);
        }
    }
}
