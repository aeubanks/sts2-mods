using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Champ.Powers;

public sealed class UltimateFormPower : ChampPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ChampCard.UltimateHoverTip
    ];

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (Amount <= 1)
        {
            Flash();
            await PowerCmd.Remove(this);
            if (Owner.Player is { } player && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await ChampStances.ExitStanceAsync(player, choiceContext);
            }
        }
        else
        {
            await PowerCmd.Decrement(this);
        }
    }
}
