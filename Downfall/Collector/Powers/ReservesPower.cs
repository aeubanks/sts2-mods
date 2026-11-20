using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Powers;

/// <summary>
/// Reserves (ReservesPower): Persistent extra Energy stored across turns.
/// Uses the exact algebraic identity R_new = min(R, (E + R) - spent):
/// GainReservesAsync adds Amount to both ReservesPower and pcs.Energy so regular Energy is spent first
/// and Reserves automatically cover any shortfall (including X-cost cards).
/// </summary>
public sealed class ReservesPower : CollectorPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    private async Task ClampToCurrentEnergyAsync()
    {
        if (Owner?.Player?.PlayerCombatState is not { } pcs
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (pcs.Energy < Amount)
        {
            int deficit = Amount - Math.Max(0, pcs.Energy);
            if (deficit >= Amount)
                await PowerCmd.Remove(this);
            else if (deficit > 0)
                await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -deficit, null, null);
        }
    }

    public override async Task AfterEnergySpent(CardModel card, int amount)
    {
        if (card.Owner != Owner.Player)
            return;

        await ClampToCurrentEnergyAsync();
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player)
            return;

        await ClampToCurrentEnergyAsync();
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner))
            return;

        await ClampToCurrentEnergyAsync();
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || Owner.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        // Only add Reserves back onto reset turn Energy when Energy actually reset;
        // if Ice Cream / ShouldPlayerResetEnergy == false, AddMaxEnergyToCurrent() already preserved E + R.
        if (Hook.ShouldPlayerResetEnergy(combatState, player))
        {
            Flash();
            await PlayerCmd.GainEnergy(Amount, player);
        }
    }
}
