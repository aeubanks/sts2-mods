using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.sts2.Core.Nodes.TopBar;

namespace Downfall.Gremlins;

/// <summary>
/// Shows all Gremlins' individual HP in the hover tooltip when hovering the top-bar HP indicator
/// as The Gremlins (matching StS 1 Downfall).
/// </summary>
[HarmonyPatch(typeof(NTopBarHp), "OnFocus")]
internal static class GremlinTopBarHpPatch
{
    private static readonly FieldInfo? PlayerField =
        typeof(NTopBarHp).GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic);

    [HarmonyPostfix]
    private static void Postfix(NTopBarHp __instance)
    {
        if (PlayerField?.GetValue(__instance) is not Player player || player.Character is not TheGremlins)
            return;

        var tips = new IHoverTip[]
        {
            new HoverTip(
                new LocString("static_hover_tips", "HIT_POINTS.title"),
                new LocString("static_hover_tips", "HIT_POINTS.description")),
            GremlinMob.BuildMobStatusHoverTip(player)
        };

        NHoverTipSet.Remove(__instance);
        NHoverTipSet.CreateAndShow(__instance, tips)
            ?.SetGlobalPosition(__instance.GlobalPosition + new Vector2(0f, __instance.Size.Y + 20f));
    }
}
