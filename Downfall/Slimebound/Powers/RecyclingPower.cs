using Downfall.Slimebound.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Powers;

public sealed class RecyclingPower : SlimePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Lick>()
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
        await SlimeMechanics.ExhumeLicksAsync(choiceContext, player, Amount);
    }
}
