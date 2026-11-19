using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Powers;

public sealed class MadGremlinPower : GremlinActivePower
{
    public override GremlinKind Kind => GremlinKind.Mad;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
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
            || dealer is null
            || dealer == Owner
            || !props.IsPoweredAttack()
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<FlexPotionPower>(choiceContext, Owner, Amount, Owner, null);
    }
}
