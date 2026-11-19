using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Champ.Powers;

public sealed class EnGardePower : ChampPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public override async Task AfterBlockBroken(PlayerChoiceContext choiceContext, Creature target, Creature? breaker)
    {
        if (target != Owner || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await PowerCmd.Apply<BlockNextTurnPower>(choiceContext, Owner, Amount, Owner, null);
        await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Remove(this);
        }
    }
}
