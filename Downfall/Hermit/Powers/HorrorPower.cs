using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hermit.Powers;

public sealed class HorrorPower : HermitPower
{
    protected override string IconKey => "horror_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BruisePower>()
    ];

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(Owner) && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            Flash();
            await PowerCmd.Decrement(this);
        }
    }
}
