using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Guardian.Powers;

public sealed class StasisEnginePower : GuardianPower
{
    private int _zeroCostPlayedThisTurn;
    private bool _triggeredThisTurn;
    private Dictionary<CardPlay, int> _capturedAmount = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _zeroCostPlayedThisTurn = 0;
        _triggeredThisTurn = false;
        _capturedAmount = [];
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            _zeroCostPlayedThisTurn = 0;
            _triggeredThisTurn = false;
            _capturedAmount.Clear();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Amount > 0 && cardPlay.Card.Owner?.Creature == Owner)
        {
            _capturedAmount[cardPlay] = Amount;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_capturedAmount.Remove(cardPlay, out int bonus)
            || bonus <= 0
            || _triggeredThisTurn
            || cardPlay.Card.Owner?.Creature != Owner
            || cardPlay.Card.EnergyCost.CostsX
            || cardPlay.Resources.EnergyValue != 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Player is not { } player)
        {
            return;
        }

        _zeroCostPlayedThisTurn++;
        if (_zeroCostPlayedThisTurn >= 3)
        {
            _triggeredThisTurn = true;
            _zeroCostPlayedThisTurn = 0;
            Flash();
            await PlayerCmd.GainEnergy(bonus, player);
            if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await CardPileCmd.Draw(choiceContext, bonus, player);
            }
        }
    }
}
