using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Core;

public interface ITempHpDepletedListener
{
    bool PreventsOverflowDamageOnTempHpDepleted => false;
    Task OnTempHpDepletedAsync();
}

/// <summary>
/// Reusable Temporary HP buff shared across Downfall characters (Gremlins, Guardian, Slime Boss, etc.).
/// Absorbs unblocked HP loss in ModifyHpLostBeforeOsty so it layers deterministically before
/// AfterOsty (ScatterPower) and AfterOstyLate (ShortStature / PetGhost / Rebirth).
/// </summary>
public sealed class DownfallTempHpPower : DownfallPower
{
    private int _pendingAbsorbed;
    private bool _pendingDepleted;

    protected override string CharacterKey => "shared";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pendingAbsorbed = 0;
        _pendingDepleted = false;
    }

    public override decimal ModifyHpLostBeforeOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m || Amount <= 0 || !CombatManager.Instance.IsInProgress)
        {
            _pendingAbsorbed = 0;
            _pendingDepleted = false;
            return amount;
        }

        int incoming = (int)decimal.Truncate(amount);
        if (incoming <= 0)
        {
            _pendingAbsorbed = 0;
            _pendingDepleted = false;
            return amount;
        }

        int absorbed = Math.Min(Amount, incoming);
        _pendingAbsorbed = absorbed;
        _pendingDepleted = absorbed >= Amount;

        if (_pendingDepleted
            && Owner.Powers.OfType<ITempHpDepletedListener>().Any(l => l.PreventsOverflowDamageOnTempHpDepleted))
        {
            return 0m;
        }

        return incoming - absorbed;
    }

    public override async Task AfterModifyingHpLostBeforeOsty()
    {
        int absorbed = _pendingAbsorbed;
        bool depleted = _pendingDepleted;
        _pendingAbsorbed = 0;
        _pendingDepleted = false;

        if (absorbed <= 0 || Amount <= 0)
            return;

        Flash();
        int remaining = Amount - absorbed;
        if (remaining <= 0)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -absorbed, null, null);
        }

        if (depleted && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            foreach (var listener in Owner.Powers.OfType<ITempHpDepletedListener>().ToList())
            {
                if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    break;
                await listener.OnTempHpDepletedAsync();
            }
        }
    }
}
