using BaseLib.Abstracts;
using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Slimebound.Powers;

public sealed class ComboAttackPower : SlimePower, IHasSecondAmount
{
    private int _remainingThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int RemainingThisTurn => _remainingThisTurn;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.CommandHoverTip
    ];

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
        if (ReferenceEquals(power, this) && amount > 0m)
        {
            _remainingThisTurn += (int)amount;
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

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_remainingThisTurn <= 0
            || cardPlay.Card.Owner?.Creature != Owner
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Player is not { } player)
        {
            return;
        }

        if (cardPlay.Card.TargetType is not (TargetType.AnyEnemy or TargetType.AllEnemies or TargetType.RandomEnemy))
            return;

        if (SlimeMechanics.GetLeadingSlime(player) is null)
            return;

        _remainingThisTurn--;
        this.InvokeSecondAmountChanged();
        Flash();
        await SlimeMechanics.CommandAsync(choiceContext, player, count: 1, applyMinionMaster: false);
    }
}
