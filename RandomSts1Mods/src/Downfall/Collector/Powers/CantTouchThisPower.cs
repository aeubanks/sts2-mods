using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Powers;

/// <summary>
/// Can't Touch This (CantTouchThisPower): Whenever you fully block an attack, apply Amount Doom to the attacker.
/// </summary>
public sealed class CantTouchThisPower : CollectorPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.DoomHoverTip
    ];

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || !props.IsPoweredAttack()
            || result.UnblockedDamage > 0
            || result.BlockedDamage <= 0
            || dealer is not { IsAlive: true }
            || dealer.Side == Owner.Side
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await CollectorMechanics.ApplyDoomAsync(choiceContext, dealer, Amount, Owner, null);
    }
}
