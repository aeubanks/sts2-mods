using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Orbs;

namespace Downfall.Core;

/// <summary>
/// Allows non-Spine Sprite2D DownfallOrb visuals inside native NOrb slots without crashing on
/// NOrb.UpdateVisuals's unconditional SpineSkeleton lookup, while updating Passive/Evoke labels cleanly.
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

            var curTween = (Tween?)CurTweenField.GetValue(__instance);
            curTween?.Kill();
            var tween = __instance.CreateTween();
            tween.TweenProperty(sprite, "scale", Vector2.One, 0.35)
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
            sprite.Modulate = orb.SpriteTint;

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
                passiveLabel.SetTextAutoSize(orb.PassiveVal.ToString("0"));
                evokeLabel.SetTextAutoSize(orb.EvokeVal.ToString("0"));
            }
            else
            {
                passiveLabel.Visible = true;
                evokeLabel.Visible = false;
                passiveLabel.SetTextAutoSize(orb.PassiveVal.ToString("0"));
            }
        }

        return false;
    }
}
