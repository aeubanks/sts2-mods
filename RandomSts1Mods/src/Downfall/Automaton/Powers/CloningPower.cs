using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Merge (CloningPower): The next time you Encode a card, Encode Amount extra copies of it.
/// </summary>
public sealed class CloningPower : BronzePower, IEncodeCopyProvider
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.EncodeHoverTip
    ];

    public async Task<int> ConsumeExtraEncodeCopiesAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        BronzeCard card,
        bool isFromCardPlay)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return 0;
        }

        int copies = Amount;
        Flash();
        await PowerCmd.Remove(this);
        return copies;
    }
}
