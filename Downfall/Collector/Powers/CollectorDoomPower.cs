using BaseLib.Hooks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Powers;

/// <summary>
/// Doom (DoomPower): At the start of its turn, the creature loses HP equal to Doom (+6 if Jade Ring is held),
/// removing Doom unless it is Afflicted (has both Weak and Vulnerable) or has DemisePower (which decrements by 1).
/// </summary>
public sealed class CollectorDoomPower : CollectorPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("EffectiveDamage", 0m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.AfflictedHoverTip
    ];

    public int EffectiveDamage =>
        Amount > 0 ? Amount + CollectorMechanics.GetDoomDamageBonus(Owner?.CombatState) : 0;

    private void RefreshEffectiveDamageVar()
    {
        if (IsMutable)
            DynamicVars["EffectiveDamage"].BaseValue = EffectiveDamage;
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        RefreshEffectiveDamageVar();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this)
            RefreshEffectiveDamageVar();
        return Task.CompletedTask;
    }

    public override IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        if (context.Creature != Owner || EffectiveDamage <= 0)
            return [];

        return
        [
            new HealthBarForecastSegment(
                EffectiveDamage,
                new Color(0.62f, 0.25f, 0.85f, 1.0f),
                HealthBarForecastDirection.FromRight)
        ];
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != Owner.Side || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int damage = EffectiveDamage;
        if (damage <= 0)
            return;

        var target = Owner;
        var dealer = Applier ?? Owner;

        Flash();

        // Update/remove Doom or Demise BEFORE dealing damage to avoid death/revive re-entrancy.
        if (!CollectorMechanics.IsAfflicted(target))
        {
            var demise = target.GetPower<CollectorDemisePower>();
            if (demise is not null)
                await PowerCmd.Decrement(demise);
            else
                await PowerCmd.Remove(this);
        }

        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CreatureCmd.Damage(
                choiceContext,
                target,
                damage,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer);
        }
    }
}
