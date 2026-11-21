using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Intensity Down (LoseIntensityPower): At the end of your turn, lose Amount Intensity.
/// </summary>
public sealed class LoseIntensityPower : HexaPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.IntensityHoverTip
    ];

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side
            || !participants.Contains(Owner)
            || !Owner.IsAlive
            || !Owner.Powers.Contains(this)
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int loss = Amount;
        Flash();
        await PowerCmd.Remove(this);
        if (loss > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<EnhancePower>(choiceContext, Owner, -loss, Owner, null);
    }
}
