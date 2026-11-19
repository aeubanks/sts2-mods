using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Powers;

public sealed class StrengthReturnPower : AwakenedPower
{
    protected override string IconKey => "strength_return_power";

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
        if (target != Owner
            || !props.IsPoweredAttack()
            || dealer is not { IsPlayer: true, IsAlive: true }
            || Amount <= 0
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<FlexPotionPower>(choiceContext, dealer, Amount, dealer, null);
    }
}
