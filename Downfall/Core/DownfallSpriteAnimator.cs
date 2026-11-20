using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace Downfall.Core;

public enum DownfallIdleStyle
{
    Grounded,
    Floating,
    FloatingMystical
}

/// <summary>
/// Drives subtle idle motion plus Attack/Cast/Hit/Death and character-specific transition
/// animations for Sprite2D-based Downfall characters (which have no Spine animator).
/// Attached as a child of the character's Sprite2D so it is freed automatically with the sprite.
/// </summary>
public sealed partial class DownfallSpriteAnimator : Node
{
    public const string AnimatorNodeName = "DownfallSpriteAnimator";

    private Sprite2D? _sprite;
    private Vector2 _basePosition;
    private Vector2 _baseScale = Vector2.One;
    private DownfallIdleStyle _idleStyle = DownfallIdleStyle.Grounded;
    private Tween? _actionTween;
    private float _idleTime;
    private bool _isDead;

    public static DownfallSpriteAnimator? ForCreature(Creature? creature)
    {
        if (creature is null)
            return null;

        var body = NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals?.Body as Sprite2D;
        return body?.GetNodeOrNull<DownfallSpriteAnimator>(AnimatorNodeName);
    }

    internal static DownfallSpriteAnimator? Attach(Sprite2D sprite, DownfallIdleStyle idleStyle)
    {
        if (NonInteractiveMode.IsActive || !GodotObject.IsInstanceValid(sprite))
            return null;

        var existing = sprite.GetNodeOrNull<DownfallSpriteAnimator>(AnimatorNodeName);
        if (existing is not null)
            return existing;

        var animator = new DownfallSpriteAnimator
        {
            Name = AnimatorNodeName,
            _sprite = sprite,
            _basePosition = sprite.Position,
            _baseScale = sprite.Scale,
            _idleStyle = idleStyle
        };
        sprite.AddChild(animator);
        return animator;
    }

    public override void _ExitTree()
    {
        KillActionTween();
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (_isDead || _sprite is null || !GodotObject.IsInstanceValid(_sprite))
            return;

        if (_actionTween is not null && GodotObject.IsInstanceValid(_actionTween) && _actionTween.IsRunning())
            return;

        _idleTime += (float)delta;

        switch (_idleStyle)
        {
            case DownfallIdleStyle.Floating:
            {
                float wave = Mathf.Sin(_idleTime * 2.1f);
                _sprite.Position = _basePosition + new Vector2(0f, wave * 7f);
                _sprite.Scale = _baseScale;
                _sprite.Rotation = 0f;
                break;
            }
            case DownfallIdleStyle.FloatingMystical:
            {
                float wave = Mathf.Sin(_idleTime * 2.2f);
                float sway = Mathf.Sin(_idleTime * 1.35f);
                float pulse = 1f + Mathf.Sin(_idleTime * 2.8f) * 0.024f;
                _sprite.Position = _basePosition + new Vector2(0f, wave * 8f);
                _sprite.Scale = _baseScale * pulse;
                _sprite.Rotation = sway * 0.045f;
                break;
            }
            default:
            {
                // Subtle breathing squash-and-stretch with slight Y compensation so feet stay grounded.
                float breath = Mathf.Sin(_idleTime * 2.3f);
                _sprite.Scale = new Vector2(
                    _baseScale.X * (1f + breath * 0.016f),
                    _baseScale.Y * (1f - breath * 0.020f));
                _sprite.Position = _basePosition + new Vector2(0f, breath * 3.0f);
                _sprite.Rotation = 0f;
                break;
            }
        }
    }

    public bool IsActionRunning =>
        _actionTween is not null && GodotObject.IsInstanceValid(_actionTween) && _actionTween.IsRunning();

    public void PlayTrigger(string trigger)
    {
        if (NonInteractiveMode.IsActive || _sprite is null || !GodotObject.IsInstanceValid(_sprite) || !_sprite.IsInsideTree())
            return;

        if (string.Equals(trigger, "Dead", StringComparison.OrdinalIgnoreCase))
        {
            PlayDeath();
            return;
        }

        if (string.Equals(trigger, "Revive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trigger, "Idle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trigger, "Relaxed", StringComparison.OrdinalIgnoreCase))
        {
            RestoreIdle();
            return;
        }

        if (_isDead)
            return;

        if (trigger.Contains("Hit", StringComparison.OrdinalIgnoreCase))
        {
            PlayHit();
        }
        else if (trigger.Contains("Cast", StringComparison.OrdinalIgnoreCase))
        {
            PlayCast();
        }
        else if (trigger.Contains("Attack", StringComparison.OrdinalIgnoreCase)
            || trigger.Contains("Slash", StringComparison.OrdinalIgnoreCase)
            || trigger.Contains("Strike", StringComparison.OrdinalIgnoreCase))
        {
            PlayAttack();
        }
    }

    public void PlayAttack()
    {
        if (!CanPlayAction(out var sprite))
            return;

        float speed = GetSpeedScale();
        var tween = BeginActionTween(sprite);

        // Wind-up pull-back
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "position", _basePosition + new Vector2(-10f, 3f), 0.04 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", new Vector2(_baseScale.X * 1.06f, _baseScale.Y * 0.94f), 0.04 * speed);
        tween.TweenProperty(sprite, "rotation", -0.04f, 0.04 * speed);

        // Forward attack lunge
        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "position", _basePosition + new Vector2(54f, -8f), 0.11 * speed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", new Vector2(_baseScale.X * 1.09f, _baseScale.Y * 0.95f), 0.11 * speed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", 0.09f, 0.11 * speed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);

        // Return smoothly
        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "position", _basePosition, 0.17 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", _baseScale, 0.17 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", 0f, 0.17 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public void PlayCast(bool onlyIfIdle = false)
    {
        if (onlyIfIdle && IsActionRunning)
            return;
        if (!CanPlayAction(out var sprite))
            return;

        float speed = GetSpeedScale();
        var tween = BeginActionTween(sprite);

        // Upward hop & subtle luminous cast pulse
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "position", _basePosition + new Vector2(0f, -18f), 0.11 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", new Vector2(_baseScale.X * 0.95f, _baseScale.Y * 1.07f), 0.11 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", new Color(1.26f, 1.26f, 1.22f, 1f), 0.09 * speed);

        // Settle back
        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "position", _basePosition, 0.16 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", _baseScale, 0.16 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", Colors.White, 0.16 * speed);
    }

    public void PlayHit()
    {
        if (!CanPlayAction(out var sprite))
            return;

        float speed = GetSpeedScale();
        var tween = BeginActionTween(sprite);

        // Knockback & red-white recoil flash
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "position", _basePosition + new Vector2(-26f, -4f), 0.06 * speed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", -0.08f, 0.06 * speed);
        tween.TweenProperty(sprite, "scale", new Vector2(_baseScale.X * 0.92f, _baseScale.Y * 1.06f), 0.06 * speed);
        tween.TweenProperty(sprite, "modulate", new Color(1.55f, 0.72f, 0.72f, 1f), 0.05 * speed);

        // Quick counter-shake
        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "position", _basePosition + new Vector2(8f, 0f), 0.08 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(sprite, "rotation", 0.03f, 0.08 * speed);

        // Settle
        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "position", _basePosition, 0.12 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", 0f, 0.12 * speed);
        tween.TweenProperty(sprite, "scale", _baseScale, 0.12 * speed);
        tween.TweenProperty(sprite, "modulate", Colors.White, 0.12 * speed);
    }

    public void PlayGuardianModeShift(bool defensive)
    {
        if (!CanPlayAction(out var sprite))
            return;

        float speed = GetSpeedScale();
        var tween = BeginActionTween(sprite);

        // Start from a curled/rotated burst so the synchronous texture swap animates seamlessly.
        sprite.Scale = defensive
            ? new Vector2(_baseScale.X * 1.25f, _baseScale.Y * 0.68f)
            : new Vector2(_baseScale.X * 0.74f, _baseScale.Y * 1.22f);
        sprite.Rotation = defensive ? -0.45f : 0.35f;
        sprite.Position = _basePosition + new Vector2(0f, defensive ? 12f : -16f);
        sprite.Modulate = defensive
            ? new Color(0.95f, 1.35f, 1.55f, 1f)
            : new Color(1.45f, 1.30f, 0.90f, 1f);

        tween.SetParallel(true);
        tween.TweenProperty(sprite, "scale", new Vector2(_baseScale.X * 1.08f, _baseScale.Y * 0.94f), 0.18 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", 0f, 0.22 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "position", _basePosition, 0.20 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", Colors.White, 0.24 * speed);

        tween.Chain();
        tween.TweenProperty(sprite, "scale", _baseScale, 0.08 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public void PlayChampStanceTransition(Color auraColor)
    {
        if (!CanPlayAction(out var sprite))
            return;

        float speed = GetSpeedScale();
        var tween = BeginActionTween(sprite);

        sprite.Scale = new Vector2(_baseScale.X * 0.88f, _baseScale.Y * 1.14f);
        sprite.Position = _basePosition + new Vector2(0f, -14f);
        sprite.Modulate = auraColor;

        tween.SetParallel(true);
        tween.TweenProperty(sprite, "scale", new Vector2(_baseScale.X * 1.10f, _baseScale.Y * 0.93f), 0.14 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "position", _basePosition, 0.16 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", Colors.White, 0.24 * speed);

        tween.Chain();
        tween.TweenProperty(sprite, "scale", _baseScale, 0.08 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public void PlayGremlinSwap(bool isNobTransform)
    {
        if (!CanPlayAction(out var sprite))
            return;

        float speed = GetSpeedScale();
        var tween = BeginActionTween(sprite);

        if (isNobTransform)
        {
            sprite.Scale = _baseScale * 0.55f;
            sprite.Position = _basePosition + new Vector2(0f, -24f);
            sprite.Modulate = new Color(1.60f, 0.72f, 0.72f, 1f);

            tween.SetParallel(true);
            tween.TweenProperty(sprite, "scale", _baseScale * 1.22f, 0.16 * speed)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(sprite, "position", _basePosition, 0.18 * speed)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(sprite, "modulate", Colors.White, 0.28 * speed);

            tween.Chain();
            tween.TweenProperty(sprite, "scale", _baseScale, 0.10 * speed)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            return;
        }

        // Crisp tag-in hop bounce from behind
        sprite.Position = _basePosition + new Vector2(-28f, -24f);
        sprite.Scale = new Vector2(_baseScale.X * 0.88f, _baseScale.Y * 1.14f);
        sprite.Rotation = 0.08f;

        tween.SetParallel(true);
        tween.TweenProperty(sprite, "position", _basePosition, 0.15 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", 0f, 0.15 * speed);
        tween.TweenProperty(sprite, "scale", new Vector2(_baseScale.X * 1.16f, _baseScale.Y * 0.85f), 0.15 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

        tween.Chain();
        tween.TweenProperty(sprite, "scale", _baseScale, 0.09 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public void PlayHexaPulse(Color flashColor, float spinRad = 0.22f)
    {
        if (!CanPlayAction(out var sprite))
            return;

        float speed = GetSpeedScale();
        var tween = BeginActionTween(sprite);

        tween.SetParallel(true);
        tween.TweenProperty(sprite, "rotation", spinRad, 0.12 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", _baseScale * 1.11f, 0.10 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", flashColor, 0.09 * speed);

        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "rotation", 0f, 0.18 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "scale", _baseScale, 0.18 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", Colors.White, 0.18 * speed);
    }

    private void PlayDeath()
    {
        if (_sprite is null || !GodotObject.IsInstanceValid(_sprite) || !_sprite.IsInsideTree())
            return;

        _isDead = true;
        float speed = GetSpeedScale();
        var tween = BeginActionTween(_sprite);

        tween.SetParallel(true);
        tween.TweenProperty(_sprite, "rotation", -0.34f, 0.35 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_sprite, "position", _basePosition + new Vector2(-14f, 28f), 0.35 * speed)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_sprite, "modulate", new Color(0.55f, 0.55f, 0.55f, 0.72f), 0.35 * speed);
    }

    private void RestoreIdle()
    {
        _isDead = false;
        KillActionTween();
        if (_sprite is not null && GodotObject.IsInstanceValid(_sprite))
        {
            _sprite.Position = _basePosition;
            _sprite.Scale = _baseScale;
            _sprite.Rotation = 0f;
            _sprite.Modulate = Colors.White;
        }
    }

    private bool CanPlayAction(out Sprite2D sprite)
    {
        if (NonInteractiveMode.IsActive || _isDead || _sprite is null || !GodotObject.IsInstanceValid(_sprite) || !_sprite.IsInsideTree())
        {
            sprite = null!;
            return false;
        }
        sprite = _sprite;
        return true;
    }

    private Tween BeginActionTween(Sprite2D sprite)
    {
        KillActionTween();
        sprite.Position = _basePosition;
        sprite.Scale = _baseScale;
        sprite.Rotation = 0f;
        sprite.Modulate = Colors.White;
        _actionTween = sprite.CreateTween();
        return _actionTween;
    }

    private void KillActionTween()
    {
        if (_actionTween is not null && GodotObject.IsInstanceValid(_actionTween) && _actionTween.IsRunning())
            _actionTween.Kill();
        _actionTween = null;
    }

    private static float GetSpeedScale() =>
        SaveManager.Instance?.PrefsSave.FastMode == FastModeType.Fast ? 0.65f : 1.0f;
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class DownfallCreatureAnimatorAttachPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity?.Player?.Character is not DownfallCharacter character)
            return;

        if (__instance.Visuals?.Body is Sprite2D sprite)
        {
            DownfallSpriteAnimator.Attach(sprite, character.IdleStyle);
        }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class DownfallCreatureSetAnimTriggerPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance, string trigger)
    {
        if (__instance.Entity?.Player?.Character is not DownfallCharacter)
            return;

        if (__instance.Visuals?.Body is Sprite2D sprite)
        {
            sprite.GetNodeOrNull<DownfallSpriteAnimator>(DownfallSpriteAnimator.AnimatorNodeName)?.PlayTrigger(trigger);
        }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class DownfallCreatureDeathAnimPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity?.Player?.Character is not DownfallCharacter)
            return;

        if (__instance.Visuals?.Body is Sprite2D sprite)
        {
            sprite.GetNodeOrNull<DownfallSpriteAnimator>(DownfallSpriteAnimator.AnimatorNodeName)?.PlayTrigger("Dead");
        }
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartReviveAnim))]
internal static class DownfallCreatureReviveAnimPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity?.Player?.Character is not DownfallCharacter)
            return;

        if (__instance.Visuals?.Body is Sprite2D sprite)
        {
            sprite.GetNodeOrNull<DownfallSpriteAnimator>(DownfallSpriteAnimator.AnimatorNodeName)?.PlayTrigger("Revive");
        }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
internal static class DownfallSkillCastAnimPatch
{
    [HarmonyPrefix]
    private static void Prefix(CardPlay cardPlay)
    {
        if (NonInteractiveMode.IsActive)
            return;

        var card = cardPlay.Card;
        if (card.Type is not (CardType.Skill or CardType.Power))
            return;

        if (card.Owner?.Character is not DownfallCharacter)
            return;

        DownfallSpriteAnimator.ForCreature(card.Owner.Creature)?.PlayCast(onlyIfIdle: true);
    }
}
