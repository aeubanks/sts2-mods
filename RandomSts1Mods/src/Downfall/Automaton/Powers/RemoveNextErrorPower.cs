using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Remove Errors (RemoveNextErrorPower): Remove the next Compile Error Encoded.
/// </summary>
public sealed class RemoveNextErrorPower : BronzePower, IOnBeforeEncodeListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.CompileHoverTip,
        BronzeCard.EncodeHoverTip
    ];

    public async Task OnBeforeEncodeAsync(PlayerChoiceContext choiceContext, Player player, BronzeCard card)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !card.IsCompileError
            || !card.CompileEnabled
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        card.CompileEnabled = false;
        Flash();
        await PowerCmd.Decrement(this);
    }
}
