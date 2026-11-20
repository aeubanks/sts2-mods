using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Slimebound.Powers;

public sealed class DuplicatedFormPower : SlimePower, IHasSecondAmount
{
    private int _remainingThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int RemainingThisTurn => _remainingThisTurn;

    public string GetSecondAmount() => _remainingThisTurn.ToString();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _remainingThisTurn = 0;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            if (amount > 0m)
                _remainingThisTurn += (int)amount;
            else if (amount < 0m)
                _remainingThisTurn = Math.Min(_remainingThisTurn, Math.Max(0, Amount));
            this.InvokeSecondAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _remainingThisTurn = Amount;
            this.InvokeSecondAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (_remainingThisTurn <= 0
            || card.Owner?.Creature != Owner
            || card.TargetType is not (TargetType.AnyEnemy or TargetType.AllEnemies or TargetType.RandomEnemy))
        {
            return playCount;
        }

        return playCount + 1;
    }

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (_remainingThisTurn > 0)
        {
            _remainingThisTurn--;
            this.InvokeSecondAmountChanged();
            Flash();
        }
        return Task.CompletedTask;
    }
}
