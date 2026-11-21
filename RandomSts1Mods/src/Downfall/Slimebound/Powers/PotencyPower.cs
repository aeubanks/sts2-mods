using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Slimebound.Powers;

public sealed class PotencyPower : SlimePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("SecondaryBonus", 1m)
    ];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SyncSecondaryBonus();
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
            SyncSecondaryBonus();
            SlimeMechanics.RefreshOrbVisuals(Owner.Player);
        }
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        SlimeMechanics.RefreshOrbVisuals(oldOwner.Player);
        return Task.CompletedTask;
    }

    private void SyncSecondaryBonus()
    {
        DynamicVars["SecondaryBonus"].BaseValue = (int)Math.Ceiling(Amount / 2.0);
    }
}
