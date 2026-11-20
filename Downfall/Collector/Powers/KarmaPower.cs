using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Powers;

/// <summary>
/// Karma (KarmaPower): At the end of your turn, gain Amount Block, plus Amount more for each Afflicted enemy.
/// Combines Karma.java's equal Metallicize + Afflicted Block into a single clean power (StS 2 has no MetallicizePower).
/// </summary>
public sealed class KarmaPower : CollectorPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        CollectorCard.AfflictedHoverTip
    ];

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side
            || !participants.Contains(Owner)
            || Amount <= 0
            || !Owner.IsAlive
            || CombatState is null
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int afflictedEnemies = CombatState.HittableEnemies.Count(CollectorMechanics.IsAfflicted);
        int totalBlock = Amount * (1 + afflictedEnemies);
        Flash();
        await CreatureCmd.GainBlock(Owner, totalBlock, ValueProp.Unpowered, null);
    }
}
