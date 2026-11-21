using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Powers;

public sealed class SlimeSacrificePower : SlimePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.AbsorbHoverTip
    ];

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || Amount <= 0
            || amount <= 0m
            || !CombatManager.Instance.IsInProgress
            || !props.IsPoweredAttack()
            || dealer is null
            || dealer.Side == Owner.Side
            || Owner.Player is not { } player
            || SlimeMechanics.GetLeadingSlime(player) is null)
        {
            return amount;
        }

        return 0m;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        if (Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding || Owner.Player is not { } player)
            return;

        Flash();
        await PowerCmd.Decrement(this);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await SlimeMechanics.AbsorbLeadingSlimeAsync(new ThrowingPlayerChoiceContext(), player);
        }
    }
}
