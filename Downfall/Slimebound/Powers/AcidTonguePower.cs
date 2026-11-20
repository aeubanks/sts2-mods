using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Slimebound.Powers;

public sealed class AcidTonguePower : SlimePower, IAcidTongueProvider
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public int ExtraGoopAmount => Amount;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.GoopHoverTip
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SlimeMechanics.RefreshOrbVisuals(Owner.Player);
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            SlimeMechanics.RefreshOrbVisuals(Owner.Player);
        }
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        SlimeMechanics.RefreshOrbVisuals(oldOwner.Player);
        return Task.CompletedTask;
    }
}
