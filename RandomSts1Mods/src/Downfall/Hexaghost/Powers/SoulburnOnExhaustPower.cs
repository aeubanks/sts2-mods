using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Soulburn Infestation (SoulburnOnExhaustPower): Whenever a card is Exhausted this turn,
/// apply Amount Soulburn to this enemy. Expires after the player's turn ends (after Ethereal exhausts).
/// </summary>
public sealed class SoulburnOnExhaustPower : HexaPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.SoulburnHoverTip
    ];

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (!Owner.IsAlive
            || Amount <= 0
            || card.Owner is null
            || card.Owner.Creature.Side == Owner.Side
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await HexaMechanics.ApplySoulburnAsync(choiceContext, Owner, Amount, card.Owner.Creature);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Remove(this);
    }
}
