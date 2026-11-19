using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Powers;

public sealed class FuriousPower : GremlinPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public override async Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (creature != Owner
            || amount <= 0m
            || Owner.CombatState is not { } combatState
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        var ctx = new ThrowingPlayerChoiceContext();
        await CreatureCmd.LoseBlock(ctx, Owner, amount, Owner);

        if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count > 0)
        {
            await CreatureCmd.Damage(
                ctx,
                enemies,
                amount,
                ValueProp.Unpowered,
                dealer: Owner,
                cardSource: null,
                cardPlay: null);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            await PowerCmd.Remove(this);
        }
    }
}
