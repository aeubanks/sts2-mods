using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Devil's Dance (PastPower): The first Amount times you Advance or Retract each turn
/// (excluding end-of-turn auto-Advance), draw 1 card.
/// </summary>
public sealed class PastPower : HexaPower, IGhostflameAdvanceListener, IGhostflameRetractListener
{
    private int _activationsThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.AdvanceHoverTip,
        HexaCard.RetractHoverTip
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            _activationsThisTurn = 0;
        return Task.CompletedTask;
    }

    public async Task OnGhostflameAdvancedAsync(PlayerChoiceContext choiceContext, Player player, bool isEndTurn)
    {
        if (isEndTurn)
            return;
        await TryTriggerAsync(choiceContext, player);
    }

    public async Task OnGhostflameRetractedAsync(PlayerChoiceContext choiceContext, Player player)
    {
        await TryTriggerAsync(choiceContext, player);
    }

    private async Task TryTriggerAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (_activationsThisTurn < Amount)
        {
            Flash();
            await CardPileCmd.Draw(choiceContext, 1m, player);
        }
        _activationsThisTurn++;
    }
}
