using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Orbs;

namespace Downfall.Core;

/// <summary>
/// Allows non-Spine Sprite2D DownfallOrb visuals inside native NOrb slots without crashing on
/// NOrb.UpdateVisuals's unconditional SpineSkeleton lookup, while updating Passive/Evoke labels
/// and playing custom passive trigger animations cleanly.
/// </summary>
[HarmonyPatch(typeof(NOrb), nameof(NOrb.UpdateVisuals))]
internal static class DownfallOrbPatch
{
    private static readonly FieldInfo SpriteField = AccessTools.Field(typeof(NOrb), "_sprite");
    private static readonly FieldInfo CurTweenField = AccessTools.Field(typeof(NOrb), "_curTween");
    private static readonly FieldInfo VisualContainerField = AccessTools.Field(typeof(NOrb), "_visualContainer");
    private static readonly FieldInfo OutlineField = AccessTools.Field(typeof(NOrb), "_outline");
    private static readonly FieldInfo LabelContainerField = AccessTools.Field(typeof(NOrb), "_labelContainer");
    private static readonly FieldInfo PassiveLabelField = AccessTools.Field(typeof(NOrb), "_passiveLabel");
    private static readonly FieldInfo EvokeLabelField = AccessTools.Field(typeof(NOrb), "_evokeLabel");
    private static readonly FieldInfo IsLocalField = AccessTools.Field(typeof(NOrb), "_isLocal");
    private static readonly ConditionalWeakTable<Sprite2D, Tween> PassiveTweens = new();

    [HarmonyPrefix]
    private static bool Prefix(NOrb __instance, bool isEvoking)
    {
        if (__instance.Model is not DownfallOrb orb)
            return true;

        if (!__instance.IsNodeReady() || !CombatManager.Instance.IsInProgress)
            return false;

        var sprite = (Node2D?)SpriteField.GetValue(__instance);
        var visualContainer = (Control?)VisualContainerField.GetValue(__instance);
        var outline = (TextureRect?)OutlineField.GetValue(__instance);
        var labelContainer = (Control?)LabelContainerField.GetValue(__instance);
        var passiveLabel = (MegaLabel?)PassiveLabelField.GetValue(__instance);
        var evokeLabel = (MegaLabel?)EvokeLabelField.GetValue(__instance);
        bool isLocal = (bool)(IsLocalField.GetValue(__instance) ?? true);

        if (sprite is null && visualContainer is not null)
        {
            sprite = orb.CreateSprite();
            visualContainer.AddChildSafely(sprite);
            sprite.Position = Vector2.Zero;
            SpriteField.SetValue(__instance, sprite);

            if (sprite is Sprite2D s2d)
            {
                Action<Func<Tween?>> runOrbAnim = buildTween =>
                {
                    if (!GodotObject.IsInstanceValid(s2d) || !s2d.IsInsideTree())
                        return;

                    if (GodotObject.IsInstanceValid(__instance))
                    {
                        var spawnTween = (Tween?)CurTweenField.GetValue(__instance);
                        if (spawnTween is not null && GodotObject.IsInstanceValid(spawnTween) && spawnTween.IsRunning())
                            spawnTween.Kill();
                    }

                    if (PassiveTweens.TryGetValue(s2d, out var prev) && GodotObject.IsInstanceValid(prev) && prev.IsRunning())
                        prev.Kill();

                    var activeTween = buildTween();
                    if (activeTween is not null)
                        PassiveTweens.AddOrUpdate(s2d, activeTween);
                };

                Action onPassive = () => runOrbAnim(() => orb.PlayPassiveAnimation(s2d));
                Action<string> onCustom = animName => runOrbAnim(() => orb.PlayCustomAnimation(s2d, animName));

                orb.PassiveActivated += onPassive;
                orb.CustomAnimationRequested += onCustom;

                if (orb.EnableIdleJiggle && !NonInteractiveMode.IsActive)
                {
                    s2d.AddChild(DownfallOrbIdleNode.Create(s2d, orb, __instance));
                }

                s2d.TreeExiting += () =>
                {
                    orb.PassiveActivated -= onPassive;
                    orb.CustomAnimationRequested -= onCustom;
                    if (PassiveTweens.TryGetValue(s2d, out var prev) && GodotObject.IsInstanceValid(prev) && prev.IsRunning())
                        prev.Kill();
                    PassiveTweens.Remove(s2d);
                };
            }

            var curTween = (Tween?)CurTweenField.GetValue(__instance);
            curTween?.Kill();
            var tween = __instance.CreateTween();
            tween.TweenProperty(sprite, "scale", orb.SpriteScale, 0.35)
                .From(Vector2.Zero)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            CurTweenField.SetValue(__instance, tween);
        }

        if (outline is not null)
            outline.Visible = false;
        if (labelContainer is not null)
            labelContainer.Visible = isLocal;
        if (!isLocal)
            __instance.Modulate = orb.DarkenedColor;
        if (sprite is not null)
        {
            bool passiveAnimating = sprite is Sprite2D animSprite
                && PassiveTweens.TryGetValue(animSprite, out var pTween)
                && GodotObject.IsInstanceValid(pTween)
                && pTween.IsRunning();

            if (!passiveAnimating)
                sprite.Modulate = orb.SpriteTint;

            if (sprite is Sprite2D sprite2D)
            {
                var desiredTex = PreloadManager.Cache.GetCompressedTexture2D(orb.SpriteTexturePath);
                if (sprite2D.Texture != desiredTex)
                    sprite2D.Texture = desiredTex;
            }
            var curTween = (Tween?)CurTweenField.GetValue(__instance);
            if (!passiveAnimating && (curTween is null || !curTween.IsRunning()))
                sprite.Scale = orb.SpriteScale;
        }

        if (passiveLabel is not null && evokeLabel is not null)
        {
            if (isEvoking)
            {
                passiveLabel.Visible = false;
                evokeLabel.Visible = true;
                evokeLabel.SetTextAutoSize(orb.EvokePreviewText);
            }
            else if (orb.ShowSecondaryLabel)
            {
                passiveLabel.Visible = true;
                evokeLabel.Visible = true;
                passiveLabel.SetTextAutoSize(orb.PrimaryLabelText);
                evokeLabel.SetTextAutoSize(orb.SecondaryLabelText);
            }
            else
            {
                passiveLabel.Visible = true;
                evokeLabel.Visible = false;
                passiveLabel.SetTextAutoSize(orb.PrimaryLabelText);
            }
        }

        return false;
    }

    internal static bool IsAnimating(Sprite2D sprite, NOrb? orbNode)
    {
        if (PassiveTweens.TryGetValue(sprite, out var pTween) && GodotObject.IsInstanceValid(pTween) && pTween.IsRunning())
            return true;

        if (orbNode is not null && GodotObject.IsInstanceValid(orbNode))
        {
            var curTween = (Tween?)CurTweenField.GetValue(orbNode);
            if (curTween is not null && GodotObject.IsInstanceValid(curTween) && curTween.IsRunning())
                return true;
        }

        return false;
    }
}

internal sealed partial class DownfallOrbIdleNode : Node
{
    private Sprite2D? _sprite;
    private DownfallOrb? _orb;
    private NOrb? _orbNode;
    private float _phase;
    private float _time;

    internal static DownfallOrbIdleNode Create(Sprite2D sprite, DownfallOrb orb, NOrb orbNode)
    {
        return new DownfallOrbIdleNode
        {
            Name = "DownfallOrbIdleNode",
            _sprite = sprite,
            _orb = orb,
            _orbNode = orbNode,
            _phase = (float)(sprite.GetInstanceId() % 628UL) * 0.01f
        };
    }

    public override void _Process(double delta)
    {
        if (_sprite is null || _orb is null || !GodotObject.IsInstanceValid(_sprite))
            return;

        if (DownfallOrbPatch.IsAnimating(_sprite, _orbNode))
            return;

        _time += (float)delta;
        float wobble = Mathf.Sin(_time * 3.1f + _phase);
        Vector2 baseScale = _orb.SpriteScale;
        _sprite.Scale = new Vector2(
            baseScale.X * (1f + wobble * 0.045f),
            baseScale.Y * (1f - wobble * 0.050f));
        _sprite.Position = new Vector2(0f, wobble * 1.6f);
        _sprite.Rotation = 0f;
    }
}
