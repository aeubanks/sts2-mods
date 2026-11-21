using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Powers;

public sealed class ScatterPower : GremlinPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GremlinCard.SwapHoverTip
    ];

    public override decimal ModifyHpLostAfterOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m || Amount <= 0 || !CombatManager.Instance.IsInProgress)
            return amount;

        return 0m;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        if (Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await PowerCmd.Decrement(this);
        if (Owner.Player is { } player && Owner.CombatState is { } combatState && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GremlinMob.SwapRandomAsync(player, combatState, new ThrowingPlayerChoiceContext());
        }
    }
}
