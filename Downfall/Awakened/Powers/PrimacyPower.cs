using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Powers;

public sealed class PrimacyPower : AwakenedPower
{
    private int _triggeredThisTurn;

    protected override string IconKey => "primacy_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisTurn = 0;
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

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power.Owner != Owner
            || power is not StrengthPower
            || amount <= 0m
            || _triggeredThisTurn >= Amount
            || Owner.Player is null
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisTurn++;
        Flash();
        await CardPileCmd.Draw(choiceContext, 1m, Owner.Player);
    }
}
