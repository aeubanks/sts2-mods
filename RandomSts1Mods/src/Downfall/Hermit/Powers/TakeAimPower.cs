using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hermit.Powers;

public sealed class TakeAimPower : HermitPower
{
    protected override string IconKey => "take_aim_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ConcentratedPower>()
    ];

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(Owner)
            && Owner.IsAlive
            && !CombatManager.Instance.IsOverOrEnding
            && !Owner.HasPower<ConcentratedPower>())
        {
            Flash();
            await PowerCmd.Apply<ConcentratedPower>(
                new ThrowingPlayerChoiceContext(),
                Owner,
                1m,
                Owner,
                null);
        }
    }
}
