using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Class Default (DefaultPower): Next Amount times you create a Function,
/// Encode a copy of the card that was in the first slot.
/// </summary>
public sealed class DefaultPower : BronzePower, IAfterFunctionOutputListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.FunctionHoverTip,
        BronzeCard.EncodeHoverTip
    ];

    public async Task OnAfterFunctionOutputAsync(PlayerChoiceContext choiceContext, Player player, BronzeCard? firstSlotCard)
    {
        if (player != Owner.Player
            || Amount <= 0
            || firstSlotCard is null
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Decrement(this);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            var copy = (BronzeCard)firstSlotCard.CreateClone();
            await AutomatonMechanics.EncodeCardAsync(choiceContext, player, copy, isFromCardPlay: false, isClone: false);
        }
    }
}
