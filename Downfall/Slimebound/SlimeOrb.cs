using Downfall.Core;
using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound;

public abstract class SlimeOrb : DownfallOrb
{
    protected override string CharacterKey => TheSlimeBoss.CharKey;

    public abstract int BaseDamage { get; }
    public virtual int BaseSecondary => 0;
    public virtual bool SecondaryScalesWithPotency => true;
    public override bool ShowSecondaryLabel => BaseSecondary > 0;
    public override string EvokePreviewText => "1";

    protected int UniqueDamageBonus { get; set; }

    protected int PotencyAmount =>
        IsMutable && Owner?.Creature is not null
            ? Owner.Creature.GetPowerAmount<PotencyPower>()
            : 0;

    protected virtual int ExtraDamageBonus => 0;
    protected virtual int ExtraSecondaryBonus => 0;

    // Slime minions scale with Potency (not Defect's Focus), matching DownfallSTS SpawnedSlime.applyFocus().
    public override decimal PassiveVal =>
        Math.Max(0m, BaseDamage + UniqueDamageBonus + PotencyAmount + ExtraDamageBonus);

    public override decimal EvokeVal =>
        ShowSecondaryLabel
            ? Math.Max(0m, BaseSecondary + (SecondaryScalesWithPotency ? (int)Math.Ceiling(PotencyAmount / 2.0) : 0) + ExtraSecondaryBonus)
            : 1m;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        UniqueDamageBonus = 0;
    }

    public override async Task AfterTurnStartOrbTrigger(PlayerChoiceContext choiceContext)
    {
        await TriggerPassive(choiceContext, null);
    }

    public override async Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext playerChoiceContext)
    {
        PlayEvokeSfx();
        ActivateEvoke([Owner.Creature]);
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<StrengthPower>(playerChoiceContext, Owner.Creature, 1m, Owner.Creature, null);
        }
        return [Owner.Creature];
    }

    protected Creature? PickRandomAliveEnemy()
    {
        if (Owner?.Creature.CombatState is not { } combatState)
            return null;

        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count == 0)
            return null;

        return enemies[Owner.RunState.Rng.CombatTargets.NextInt(enemies.Count)];
    }

    protected async Task DealUnpoweredDamageAsync(PlayerChoiceContext choiceContext, Creature target, decimal amount)
    {
        if (amount <= 0m || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CreatureCmd.Damage(choiceContext, target, amount, ValueProp.Unpowered, Owner.Creature);
    }

    protected async Task DealUnpoweredAoeDamageAsync(PlayerChoiceContext choiceContext, decimal amount)
    {
        if (amount <= 0m || CombatManager.Instance.IsOverOrEnding || Owner?.Creature.CombatState is not { } combatState)
            return;

        var targets = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (targets.Count == 0)
            return;

        await CreatureCmd.Damage(choiceContext, targets, amount, ValueProp.Unpowered, Owner.Creature);
    }

    protected void RefreshVisuals()
    {
        if (IsMutable)
        {
            SlimeMechanics.RefreshOrbVisuals(Owner);
        }
    }
}
