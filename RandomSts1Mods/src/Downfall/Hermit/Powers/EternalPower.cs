using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hermit.Powers;

public sealed class EternalPower : HermitPower
{
    private const int CardsPerTurn = 4;
    private int _discountedThisTurn;

    protected override string IconKey => "eternal_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _discountedThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (!fromHandDraw
            || card.Owner != Owner.Player
            || Amount <= 0
            || _discountedThisTurn >= CardsPerTurn
            || card.Keywords.Contains(CardKeyword.Unplayable))
        {
            return Task.CompletedTask;
        }

        _discountedThisTurn++;
        if (!card.EnergyCost.CostsX && card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
        {
            Flash();
            card.EnergyCost.AddThisTurn(-Amount, reduceOnly: true);
        }

        return Task.CompletedTask;
    }
}
