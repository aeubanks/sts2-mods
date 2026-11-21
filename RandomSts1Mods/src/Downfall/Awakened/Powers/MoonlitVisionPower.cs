using Downfall.Awakened.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Awakened.Powers;

public sealed class MoonlitVisionPower : AwakenedPower
{
    private int _triggeredThisTurn;

    protected override string IconKey => "moonlit_vision_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisTurn = 0;
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _triggeredThisTurn = Math.Min(Amount, Carrionmaker.CountSpellsPlayedThisTurn(Owner.Player, CombatState));
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            _triggeredThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.Card is not AwakenedSpellCard
            || _triggeredThisTurn >= Amount
            || Owner.Player is null
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisTurn++;
        Flash();
        await PlayerCmd.GainEnergy(1m, Owner.Player);
    }
}
