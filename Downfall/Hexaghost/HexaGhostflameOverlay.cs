using Downfall.Hexaghost.Orbs;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace Downfall.Hexaghost;

/// <summary>
/// Dedicated StS 1-style hexagonal Ghostflame ring directly encircling Hexaghost's body,
/// completely decoupled from Defect's OrbQueue/NOrbManager so Ghostflames and Defect orbs
/// can coexist without overlapping or evicting each other.
/// </summary>
public static class HexaGhostflameOverlay
{
    public const string WheelNodeName = "HexaGhostflameWheel";
    public const string HoverLayerNodeName = "HexaGhostflameHoverLayer";
    public const string TriggerPipsNodeName = "HexaTriggerPips";

    // Clockwise flat-top hexagon centered around Hexaghost's body (0, -200), matching DownfallSTS GhostflameHelper:
    // Slot 0 Top-Left (Searing), Slot 1 Top-Right (Crushing), Slot 2 Mid-Right (Bolstering),
    // Slot 3 Bottom-Right (Searing), Slot 4 Bottom-Left (Crushing), Slot 5 Mid-Left (Inferno).
    private static readonly Vector2[] HexOffsets =
    [
        new(-96f, -346f),
        new( 96f, -346f),
        new(182f, -200f),
        new( 96f,  -54f),
        new(-96f,  -54f),
        new(-182f, -200f)
    ];

    private const float HoverHitboxSize = 74f;
    private const float EmblemSize = 52f;
    private const float PipSpacing = 44f;

    public static void UpdateVisuals(Player? player)
    {
        if (player?.Creature is null)
            return;

        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        var visuals = creatureNode?.Visuals;
        if (creatureNode is null || visuals is null)
            return;

        var flames = HexaMechanics.GetGhostflames(player);
        var wheel = visuals.GetNodeOrNull<Node2D>(WheelNodeName);
        var hoverLayer = creatureNode.GetNodeOrNull<Control>(HoverLayerNodeName);

        if (flames.Count == 0)
        {
            if (wheel is not null)
                wheel.Visible = false;
            if (hoverLayer is not null)
                hoverLayer.Visible = false;
            return;
        }

        if (wheel is null)
        {
            wheel = CreateWheelNode();
            visuals.AddChild(wheel);
        }

        if (hoverLayer is null)
        {
            hoverLayer = CreateHoverLayer(player);
            creatureNode.AddChild(hoverLayer);
        }

        wheel.Visible = true;
        hoverLayer.Visible = true;
        hoverLayer.Position = visuals.Position;

        int activeIndex = HexaMechanics.GetActiveSlotIndex(player);

        for (int i = 0; i < HexOffsets.Length; i++)
        {
            var slot = wheel.GetNodeOrNull<Node2D>($"Flame{i}");
            var hover = hoverLayer.GetNodeOrNull<Control>($"Hover{i}");

            if (i >= flames.Count)
            {
                if (slot is not null)
                    slot.Visible = false;
                if (hover is not null)
                    hover.Visible = false;
                continue;
            }

            var flame = flames[i];
            bool isActive = i == activeIndex;
            bool isIgnited = flame.IsIgnited;

            if (slot is not null)
            {
                slot.Visible = true;
                float targetScale = isActive ? 1.18f : (isIgnited ? 1.06f : 0.94f);
                if (!slot.HasMeta("animating"))
                    slot.Scale = new Vector2(targetScale, targetScale);

                if (slot.GetNodeOrNull<HexaFlameVfxNode>("FlameVfx") is { } vfx)
                {
                    vfx.Configure(isIgnited, isActive, GetExtinguishedColor(flame));
                }

                if (slot.GetNodeOrNull<Sprite2D>("Emblem") is { } emblem)
                {
                    string texPath = flame.SpriteTexturePath;
                    if (emblem.Texture?.ResourcePath != texPath && ResourceLoader.Exists(texPath))
                    {
                        var tex = ResourceLoader.Load<Texture2D>(texPath);
                        if (tex is not null)
                        {
                            emblem.Texture = tex;
                            Vector2 sz = tex.GetSize();
                            float maxDim = Mathf.Max(sz.X, sz.Y);
                            if (maxDim > 0f)
                                emblem.Scale = Vector2.One * (EmblemSize / maxDim);
                        }
                    }

                    emblem.Modulate = isIgnited
                        ? new Color(1.08f, 1.18f, 1.05f, 0.98f)
                        : isActive
                            ? new Color(1.05f, 0.96f, 1.12f, 0.98f)
                            : new Color(0.68f, 0.70f, 0.78f, 0.85f);
                }

                if (slot.GetNodeOrNull<Label>("StatusLabel") is { } label)
                {
                    label.Text = flame.SecondaryLabelText;
                    label.AddThemeColorOverride("font_color", isIgnited
                        ? new Color(0.62f, 1.0f, 0.68f)
                        : isActive
                            ? new Color(0.95f, 0.82f, 1.0f)
                            : new Color(0.78f, 0.82f, 0.90f));
                }
            }

            if (hover is not null)
            {
                hover.Visible = true;
            }
        }

        UpdateTriggerPips(wheel, flames, activeIndex);
    }

    public static void PlaySlotAnimation(Player? player, GhostflameOrb ghostflame, bool ignited)
    {
        if (NonInteractiveMode.IsActive || player?.Creature is null)
            return;

        var flames = HexaMechanics.GetGhostflames(player);
        int slotIndex = -1;
        for (int i = 0; i < flames.Count; i++)
        {
            if (flames[i] == ghostflame)
            {
                slotIndex = i;
                break;
            }
        }
        if (slotIndex < 0)
            return;

        var visuals = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals;
        var slot = visuals?.GetNodeOrNull<Node2D>(WheelNodeName)?.GetNodeOrNull<Node2D>($"Flame{slotIndex}");
        if (slot is null || !GodotObject.IsInstanceValid(slot) || !slot.IsInsideTree())
            return;

        float speed = SaveManager.Instance?.PrefsSave.FastMode == FastModeType.Fast ? 0.65f : 1.0f;
        bool isActive = slotIndex == HexaMechanics.GetActiveSlotIndex(player);
        float restScalar = isActive ? 1.18f : (ghostflame.IsIgnited ? 1.06f : 0.94f);
        Vector2 restScale = new(restScalar, restScalar);
        Vector2 restPos = HexOffsets[slotIndex];

        slot.SetMeta("animating", true);
        var tween = slot.CreateTween();

        if (ignited)
        {
            tween.SetParallel(true);
            tween.TweenProperty(slot, "scale", restScale * 1.48f, 0.11 * speed)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(slot, "position", restPos + new Vector2(0f, -12f), 0.11 * speed)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(slot, "modulate", new Color(1.45f, 1.90f, 1.50f, 1f), 0.09 * speed);

            tween.Chain().SetParallel(true);
            tween.TweenProperty(slot, "scale", restScale, 0.20 * speed)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(slot, "position", restPos, 0.20 * speed)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(slot, "modulate", Colors.White, 0.20 * speed);
        }
        else
        {
            tween.SetParallel(true);
            tween.TweenProperty(slot, "scale", restScale * 0.78f, 0.09 * speed)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(slot, "modulate", new Color(0.72f, 0.74f, 0.92f, 0.80f), 0.09 * speed);

            tween.Chain().SetParallel(true);
            tween.TweenProperty(slot, "scale", restScale, 0.15 * speed)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(slot, "modulate", Colors.White, 0.15 * speed);
        }

        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(slot))
                slot.RemoveMeta("animating");
        }));
    }

    private static void UpdateTriggerPips(Node2D wheel, IReadOnlyList<GhostflameOrb> flames, int activeIndex)
    {
        var pipsRoot = wheel.GetNodeOrNull<Node2D>(TriggerPipsNodeName);
        if (pipsRoot is null)
            return;

        if (activeIndex < 0 || activeIndex >= flames.Count)
        {
            pipsRoot.Visible = false;
            return;
        }

        var active = flames[activeIndex];
        int req = Math.Clamp(active.TriggersRequired, 1, 3);
        int filled = active.IsIgnited ? req : Math.Clamp(active.TriggerProgress, 0, req);

        string dimPath = active.CustomIconPath;
        string brightPath = active.BrightIconPath;
        Texture2D? dimTex = ResourceLoader.Exists(dimPath) ? ResourceLoader.Load<Texture2D>(dimPath) : null;
        Texture2D? brightTex = ResourceLoader.Exists(brightPath) ? ResourceLoader.Load<Texture2D>(brightPath) : dimTex;

        pipsRoot.Visible = true;
        float startX = -((req - 1) * PipSpacing) * 0.5f;

        for (int i = 0; i < 3; i++)
        {
            var pip = pipsRoot.GetNodeOrNull<Sprite2D>($"Pip{i}");
            if (pip is null)
                continue;

            if (i >= req)
            {
                pip.Visible = false;
                continue;
            }

            pip.Visible = true;
            pip.Position = new Vector2(startX + i * PipSpacing, 0f);
            bool lit = i < filled;
            var tex = lit ? (brightTex ?? dimTex) : (dimTex ?? brightTex);
            if (tex is not null)
            {
                pip.Texture = tex;
                Vector2 sz = tex.GetSize();
                float maxDim = Mathf.Max(sz.X, sz.Y);
                if (maxDim > 0f)
                    pip.Scale = Vector2.One * (36f / maxDim);
            }
            pip.Modulate = lit ? new Color(1.12f, 1.18f, 1.12f, 0.98f) : new Color(0.72f, 0.72f, 0.80f, 0.82f);
        }
    }

    private static Node2D CreateWheelNode()
    {
        var root = new Node2D
        {
            Name = WheelNodeName,
            Position = Vector2.Zero,
            ZIndex = 4
        };

        for (int i = 0; i < HexOffsets.Length; i++)
        {
            var slot = new Node2D
            {
                Name = $"Flame{i}",
                Position = HexOffsets[i]
            };

            var vfx = HexaFlameVfxNode.Create(i);
            vfx.Name = "FlameVfx";
            slot.AddChild(vfx);

            var emblem = new Sprite2D
            {
                Name = "Emblem",
                Centered = true,
                ZIndex = 1
            };
            slot.AddChild(emblem);

            var label = new Label
            {
                Name = "StatusLabel",
                Position = new Vector2(-34f, 16f),
                Size = new Vector2(68f, 22f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ZIndex = 2,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 15);
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 5);
            slot.AddChild(label);

            root.AddChild(slot);
        }

        // StS 1 overhead active-flame trigger progress pips (above top vertices of the hexagon)
        var pipsRoot = new Node2D
        {
            Name = TriggerPipsNodeName,
            Position = new Vector2(0f, -412f),
            ZIndex = 5
        };
        for (int i = 0; i < 3; i++)
        {
            pipsRoot.AddChild(new Sprite2D
            {
                Name = $"Pip{i}",
                Centered = true
            });
        }
        root.AddChild(pipsRoot);

        return root;
    }

    private static Control CreateHoverLayer(Player player)
    {
        var layer = new Control
        {
            Name = HoverLayerNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 20
        };

        for (int i = 0; i < HexOffsets.Length; i++)
        {
            int slotIndex = i;
            Vector2 offset = HexOffsets[i];
            var hover = new Control
            {
                Name = $"Hover{i}",
                Position = offset - new Vector2(HoverHitboxSize * 0.5f, HoverHitboxSize * 0.5f),
                Size = new Vector2(HoverHitboxSize, HoverHitboxSize),
                MouseFilter = Control.MouseFilterEnum.Pass
            };

            hover.MouseEntered += () =>
            {
                var flames = HexaMechanics.GetGhostflames(player);
                if (slotIndex >= flames.Count)
                    return;

                var tips = flames[slotIndex].HoverTips.ToList();
                if (tips.Count == 0)
                    return;

                NHoverTipSet.CreateAndShow(hover, tips, HoverTip.GetHoverTipAlignment(hover))?.SetFollowOwner();
            };
            hover.MouseExited += () => NHoverTipSet.Remove(hover);

            layer.AddChild(hover);
        }

        return layer;
    }

    private static Color GetExtinguishedColor(GhostflameOrb flame) => flame switch
    {
        SearingGhostflameOrb => new Color(0.55f, 0.92f, 0.52f),
        CrushingGhostflameOrb => new Color(0.95f, 0.65f, 0.52f),
        BolsteringGhostflameOrb => new Color(0.45f, 0.78f, 1.0f),
        InfernoGhostflameOrb => new Color(0.85f, 0.55f, 0.98f),
        _ => new Color(0.55f, 0.85f, 0.95f)
    };
}

/// <summary>
/// Procedural additive flickering spectral fire rendered behind each Hexaghost Ghostflame slot,
/// matching StS 1's 3 visual states: roaring ghostly emerald (Ignited), vivid purple (Active),
/// and soft type-colored spectral ember (Extinguished).
/// </summary>
internal sealed partial class HexaFlameVfxNode : Node2D
{
    private float _phase;
    private float _time;
    private bool _isIgnited;
    private bool _isActive;
    private Color _dimColor = new(0.55f, 0.85f, 0.95f);

    internal static HexaFlameVfxNode Create(int slotIndex)
    {
        var node = new HexaFlameVfxNode
        {
            _phase = slotIndex * 1.13f,
            ZIndex = 0
        };
        if (!NonInteractiveMode.IsActive)
        {
            node.Material = new CanvasItemMaterial
            {
                BlendMode = CanvasItemMaterial.BlendModeEnum.Add
            };
        }
        return node;
    }

    internal void Configure(bool isIgnited, bool isActive, Color dimColor)
    {
        _isIgnited = isIgnited;
        _isActive = isActive;
        _dimColor = dimColor;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (NonInteractiveMode.IsActive)
            return;

        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (NonInteractiveMode.IsActive)
            return;

        float t = _time * (_isIgnited ? 6.2f : (_isActive ? 4.6f : 2.8f)) + _phase;
        float pulse = 0.92f + Mathf.Sin(t) * 0.10f;
        float sway = Mathf.Sin(t * 0.73f) * 3.5f;

        Color outer;
        Color mid;
        Color core;
        float radius;
        float flameHeight;

        if (_isIgnited)
        {
            // StS 1 GhostlyFireEffect: roaring chartreuse/emerald spectral flame
            outer = new Color(0.22f, 0.95f, 0.42f, 0.42f);
            mid = new Color(0.45f, 1.0f, 0.56f, 0.55f);
            core = new Color(0.84f, 1.0f, 0.74f, 0.65f);
            radius = 38f * pulse;
            flameHeight = 46f * pulse;
        }
        else if (_isActive)
        {
            // StS 1 ActiveFireEffect: vivid mystic purple flame
            outer = new Color(0.68f, 0.22f, 0.98f, 0.44f);
            mid = new Color(0.82f, 0.42f, 1.0f, 0.54f);
            core = new Color(0.95f, 0.76f, 1.0f, 0.60f);
            radius = 36f * pulse;
            flameHeight = 42f * pulse;
        }
        else
        {
            // StS 1 GhostlyWeakColorableFireEffect: soft dim type-colored wisp
            outer = new Color(_dimColor.R, _dimColor.G, _dimColor.B, 0.22f);
            mid = new Color(_dimColor.R, _dimColor.G, _dimColor.B, 0.30f);
            core = new Color(1f, 1f, 1f, 0.22f);
            radius = 31f * pulse;
            flameHeight = 28f * pulse;
        }

        // Soft outer halo + rising spectral flame tongue (drawn as layered overlapping circles/teardrop)
        DrawCircle(Vector2.Zero, radius, outer);
        DrawCircle(new Vector2(sway * 0.4f, -flameHeight * 0.32f), radius * 0.72f, mid);
        DrawCircle(new Vector2(-sway * 0.3f, -flameHeight * 0.62f), radius * 0.44f, mid);
        DrawCircle(new Vector2(sway * 0.5f, -flameHeight * 0.88f), radius * 0.24f, core);
        DrawCircle(new Vector2(0f, -4f), radius * 0.52f, core);

        // Two subtle drifting side wisps when active or ignited
        if (_isIgnited || _isActive)
        {
            float w1 = Mathf.Sin(t * 1.3f);
            float w2 = Mathf.Cos(t * 1.1f);
            DrawCircle(new Vector2(-14f + w1 * 3f, -18f - w2 * 6f), 11f, mid);
            DrawCircle(new Vector2(14f - w2 * 3f, -16f - w1 * 6f), 10f, mid);
        }
    }
}

/// <summary>
/// Exposes Hexaghost's decoupled coordinator GhostflameOrb to StS 2's combat hook pipeline
/// so turn/card/exhaust triggers run identically without occupying Defect's OrbQueue.
/// </summary>
[HarmonyPatch(typeof(Hook), "IterateCombatHookListeners")]
internal static class HexaGhostflameHookPatch
{
    [HarmonyPostfix]
    private static IEnumerable<AbstractModel> Postfix(IEnumerable<AbstractModel> __result, ICombatState combatState)
    {
        foreach (var item in __result)
            yield return item;

        if (combatState is null)
            yield break;

        var flames = HexaCombatState.Get(combatState).Ghostflames;
        if (flames.Count > 0)
            yield return flames[0];
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class HexaGhostflameMountPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity?.Player is { Character: TheHexaghost } player)
        {
            HexaGhostflameOverlay.UpdateVisuals(player);
        }
    }
}
