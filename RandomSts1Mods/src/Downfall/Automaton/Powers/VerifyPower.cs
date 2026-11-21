using Downfall.Automaton.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Sentient Form (VerifyPower): Whenever you draw a Function, increase all numbers on it by Amount except cost.
/// </summary>
public sealed class VerifyPower : BronzePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.FunctionHoverTip
    ];

    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner != Owner.Player
            || card is not FunctionCard function
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return Task.CompletedTask;
        }

        Flash();
        function.ApplyFineTune(Amount);
        return Task.CompletedTask;
    }
}
