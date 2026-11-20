using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Powers;

public sealed class GoopArmorPower : SlimePower, IGoopConsumedListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.ConsumeHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public async Task OnGoopConsumedAsync(PlayerChoiceContext choiceContext, Creature target, int consumedAmount)
    {
        if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding || Amount <= 0)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}
