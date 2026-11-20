using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Powers;

public sealed class GoopPerTurnPower : SlimePower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.GoopHoverTip
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
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var applier = Applier?.Player ?? combatState.Players.FirstOrDefault(p => p.Creature.IsAlive);
        if (applier is null)
            return;

        Flash();
        await SlimeMechanics.ApplyGoopAsync(choiceContext, Owner, applier, Amount, null);
    }
}
