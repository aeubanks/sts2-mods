using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Powers;

public sealed class LoseSlimesPower : SlimePower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.AbsorbHoverTip
    ];

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Player is not { } player)
        {
            return;
        }

        Flash();
        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
            if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await SlimeMechanics.AbsorbAllSlimesAsync(choiceContext, player);
            }
        }
        else
        {
            await PowerCmd.Decrement(this);
        }
    }
}
