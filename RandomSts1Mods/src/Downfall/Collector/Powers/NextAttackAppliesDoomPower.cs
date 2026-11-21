using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Powers;

/// <summary>
/// Doomed Attack (NextAttackAppliesDoomPower): Your next Attack this turn also applies Amount Doom.
/// </summary>
public sealed class NextAttackAppliesDoomPower : CollectorPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.DoomHoverTip
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.Card.Type != CardType.Attack
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int doomAmount = Amount;
        Flash();
        await PowerCmd.Remove(this);

        if (CombatState is null || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        List<Creature> targets = cardPlay.Target is not null
            ? (cardPlay.Target.IsAlive ? [cardPlay.Target] : [])
            : CombatState.HittableEnemies.Where(e => e.IsAlive).ToList();

        foreach (var target in targets)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!target.IsAlive)
                continue;
            await CollectorMechanics.ApplyDoomAsync(choiceContext, target, doomAmount, Owner, cardPlay.Card);
        }
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Side
            && participants.Contains(Owner)
            && Owner.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Remove(this);
        }
    }
}
