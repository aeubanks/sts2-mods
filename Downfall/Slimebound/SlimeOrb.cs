using Downfall.Core;
using Downfall.Slimebound.Powers;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound;

public abstract class SlimeOrb : DownfallOrb
{
    protected override string CharacterKey => TheSlimeBoss.CharKey;
    public override bool EnableIdleJiggle => true;

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
        // Battle-start Slimes (AggressiveSlimeRelic, MaxSlimesRelic) do not auto-attack on Turn 1.
        if (Owner?.Creature.CombatState?.RoundNumber <= 1)
            return;

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

    public override Tween? PlayPassiveAnimation(Sprite2D sprite)
    {
        if (NonInteractiveMode.IsActive)
            return null;

        float speed = SaveManager.Instance?.PrefsSave.FastMode == FastModeType.Fast ? 0.65f : 1.0f;
        Vector2 baseScale = SpriteScale;

        sprite.Position = Vector2.Zero;
        sprite.Rotation = 0f;
        sprite.Scale = baseScale;

        var tween = sprite.CreateTween();

        // 1. Brief squash-and-coil anticipation
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "position", new Vector2(-8f, 4f), 0.045 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", new Vector2(baseScale.X * 1.18f, baseScale.Y * 0.80f), 0.045 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", -0.08f, 0.045 * speed);

        // 2. Forward hop-lunge toward enemies + bright highlight (echoing StS 1 SpawnedSlime.useFastAttackAnimation)
        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "position", new Vector2(56f, -14f), 0.14 * speed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", new Vector2(baseScale.X * 1.24f, baseScale.Y * 0.90f), 0.14 * speed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", 0.14f, 0.14 * speed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", new Color(1.55f, 1.55f, 1.40f, 1f), 0.08 * speed);

        // 3. Glide back to orb slot with soft landing squash
        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "position", Vector2.Zero, 0.17 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", 0f, 0.17 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", new Vector2(baseScale.X * 1.08f, baseScale.Y * 0.92f), 0.17 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", SpriteTint, 0.17 * speed);

        // 4. Final settle to exact rest transform & live tint (e.g. Bronze Slime's stunned dim)
        tween.Chain();
        tween.TweenProperty(sprite, "scale", baseScale, 0.06 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(sprite))
            {
                sprite.Position = Vector2.Zero;
                sprite.Rotation = 0f;
                sprite.Scale = SpriteScale;
                sprite.Modulate = SpriteTint;
            }
        }));

        return tween;
    }

    public override Tween? PlayCustomAnimation(Sprite2D sprite, string animName)
    {
        if (NonInteractiveMode.IsActive || animName != "unsquish")
            return null;

        float speed = SaveManager.Instance?.PrefsSave.FastMode == FastModeType.Fast ? 0.65f : 1.0f;
        Vector2 baseScale = SpriteScale;

        sprite.Position = new Vector2(0f, 5f);
        sprite.Rotation = 0f;
        sprite.Scale = new Vector2(baseScale.X * 1.22f, baseScale.Y * 0.74f);

        var tween = sprite.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "position", new Vector2(0f, -12f), 0.12 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", new Vector2(baseScale.X * 0.90f, baseScale.Y * 1.18f), 0.12 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", new Color(1.30f, 1.30f, 1.20f, 1f), 0.10 * speed);

        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "position", Vector2.Zero, 0.14 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", baseScale, 0.14 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", SpriteTint, 0.14 * speed);

        return tween;
    }

    protected async Task DealUnpoweredDamageAsync(PlayerChoiceContext choiceContext, Creature target, decimal amount)
    {
        if (amount <= 0m || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await Cmd.CustomScaledWait(0.08f, 0.16f);
        if (!target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CreatureCmd.Damage(choiceContext, target, amount, ValueProp.Unpowered, Owner.Creature);
    }

    protected async Task DealUnpoweredAoeDamageAsync(PlayerChoiceContext choiceContext, decimal amount)
    {
        if (amount <= 0m || CombatManager.Instance.IsOverOrEnding || Owner?.Creature.CombatState is not { } combatState)
            return;

        await Cmd.CustomScaledWait(0.08f, 0.16f);
        if (CombatManager.Instance.IsOverOrEnding)
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
