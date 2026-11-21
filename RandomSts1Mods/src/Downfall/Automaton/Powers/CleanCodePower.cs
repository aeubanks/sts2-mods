using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Clean Code (CleanCodePower): Remove all Compile Error effects from Encoded cards
/// for the next Amount Function(s) you create.
/// </summary>
public sealed class CleanCodePower : BronzePower, IPreCompileListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.CompileHoverTip,
        BronzeCard.FunctionHoverTip
    ];

    public async Task OnPreCompileAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyList<BronzeCard> encodedCards)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        foreach (var card in encodedCards)
        {
            if (card.IsCompileError)
                card.CompileEnabled = false;
        }

        Flash();
        await PowerCmd.Decrement(this);
    }
}
