using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Powers;

public sealed class MayhemFormPower : PackmasterPower
{
    private bool _activatedThisTurn;

    protected override string IconKey => "mayhem_form_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner))
            _activatedThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        cardPlay.Card.Owner == Owner.Player ? TryTriggerAsync(choiceContext) : Task.CompletedTask;

    public override Task AfterCardDiscarded(PlayerChoiceContext choiceContext, CardModel card) =>
        card.Owner == Owner.Player ? TryTriggerAsync(choiceContext) : Task.CompletedTask;

    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal) =>
        card.Owner == Owner.Player ? TryTriggerAsync(choiceContext) : Task.CompletedTask;

    private async Task TryTriggerAsync(PlayerChoiceContext choiceContext)
    {
        if (_activatedThisTurn
            || !CombatManager.Instance.IsInProgress
            || CombatManager.Instance.IsEnemyTurnStarted
            || CombatManager.Instance.EndingPlayerTurnPhaseOne
            || CombatManager.Instance.EndingPlayerTurnPhaseTwo)
        {
            return;
        }

        var player = Owner.Player;
        if (player?.PlayerCombatState is null || player.PlayerCombatState.Hand.Cards.Count > 2)
            return;

        _activatedThisTurn = true;
        Flash();
        await CardPileCmd.Draw(choiceContext, 2m, player);
        await PlayerCmd.GainEnergy(Amount, player);
    }
}
