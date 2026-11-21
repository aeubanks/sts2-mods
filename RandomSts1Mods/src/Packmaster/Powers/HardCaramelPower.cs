using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class HardCaramelPower : PackmasterPower
{
    protected override string IconKey => "hard_caramel_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (dealer == Owner
            && target != Owner
            && target.IsAlive
            && Owner.IsAlive
            && props.IsPoweredAttack()
            && !target.HasPower<VulnerablePower>())
        {
            Flash();
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, Amount, Owner, null);
        }
    }
}
