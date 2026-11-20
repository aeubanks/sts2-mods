using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Powers;

/// <summary>
/// Goop debuff: next powered player Attack deals +Amount bonus damage, triggers Consume/Goop listeners,
/// and removes Goop (unless DontRemoveOnce or PreventSlimeDecayPower is active).
/// </summary>
public sealed class SlimedPower : SlimePower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    internal bool DontRemoveOnce { get; set; }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        DontRemoveOnce = false;
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target == Owner && props.IsPoweredAttack() && dealer is { IsPlayer: true })
            return Amount;

        return 0m;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (target != Owner
            || !props.IsPoweredAttack()
            || dealer is not { IsPlayer: true }
            || dealer.Player is not { } player
            || Amount <= 0)
        {
            return;
        }

        int consumed = Amount;

        // Resolve Goop removal BEFORE notifying listeners to prevent re-entrant double-consume.
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            if (DontRemoveOnce)
            {
                DontRemoveOnce = false;
            }
            else if (Owner.Powers.OfType<IPreventGoopDecay>().FirstOrDefault() is { } preventer)
            {
                await preventer.ConsumePreventionAsync();
            }
            else
            {
                await PowerCmd.Remove(this);
            }
        }

        // AfterDamageGiven runs even on lethal hits (unlike AfterDamageReceived which StS 2 skips on kills).
        foreach (var listener in player.Relics.OfType<IGoopConsumedListener>().ToList())
        {
            if (!dealer.IsAlive)
                break;
            await listener.OnGoopConsumedAsync(choiceContext, target, consumed);
        }

        foreach (var listener in player.Creature.Powers.OfType<IGoopConsumedListener>().ToList())
        {
            if (!dealer.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnGoopConsumedAsync(choiceContext, target, consumed);
        }
    }
}
