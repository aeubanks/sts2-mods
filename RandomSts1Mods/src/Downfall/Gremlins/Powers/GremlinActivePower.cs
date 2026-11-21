using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Gremlins.Powers;

public abstract class GremlinActivePower : GremlinPower
{
    public abstract GremlinKind Kind { get; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldDie(Creature creature)
    {
        if (creature != Owner || Owner.Player is not { } player)
            return true;

        return !GremlinMob.ShouldPreventDeath(player);
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner || Owner.Player is not { } player)
            return;

        await GremlinMob.HandleActiveGremlinDeathAsync(player);
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner) && Owner.Player is { } player)
        {
            GremlinMob.UpdateCombatVisuals(player);
        }
        return Task.CompletedTask;
    }
}
