using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Awakened.Powers;

public sealed class RisingChantPower : AwakenedPower
{
    private int _triggeredThisTurn;

    protected override string IconKey => "rising_chant_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AwakenedCard.ChantHoverTip
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisTurn = 0;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            _triggeredThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    internal bool TryConsumeDoubleChant()
    {
        if (!Owner.IsAlive || _triggeredThisTurn >= Amount || CombatManager.Instance.IsOverOrEnding)
            return false;

        _triggeredThisTurn++;
        Flash();
        return true;
    }
}
