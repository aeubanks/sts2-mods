using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class IceShatterPower : PackmasterPower
{
    private bool _isTriggering;

    protected override string IconKey => "ice_shatter_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (_isTriggering || target != Owner || !Owner.IsAlive || result.UnblockedDamage <= 0 || Owner.CombatState?.CurrentSide != Owner.Side)
            return;

        _isTriggering = true;
        try
        {
            Flash();
            await CreatureCmd.Damage(
                choiceContext,
                Owner,
                Amount,
                ValueProp.Unpowered,
                dealer: null,
                cardSource: null,
                cardPlay: null);
        }
        finally
        {
            _isTriggering = false;
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}
