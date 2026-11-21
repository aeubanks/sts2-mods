using Downfall.Automaton.Orbs;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace Downfall.Automaton;

/// <summary>
/// Dedicated StS 1-style horizontal bronze Function Sequence tray beside The Automaton,
/// completely decoupled from Defect's OrbQueue/NOrbManager so Encoded cards and Defect orbs
/// can coexist cleanly without overlapping or evicting each other.
/// </summary>
public static class AutomatonFunctionOverlay
{
    public const string TrayNodeName = "AutomatonFunctionTray";
    public const string HoverLayerNodeName = "AutomatonFunctionHoverLayer";

    private const string SequenceSlotTexturePath = "res://Downfall/images/automaton/ui/sequence_slot.png";
    private const string FallbackOrbTexturePath = "res://Downfall/images/automaton/orbs/encoded_card_orb.png";

    // Positioned neatly to the upper-right of Automaton (body x in [-120, 120]), matching StS 1 FunctionHelper
    // and staying completely clear of Defect's overhead orb arc.
    private static readonly Vector2 TrayPosition = new(240f, -255f);
    private static readonly float[] SlotXOffsets = [-72f, 0f, 72f];
    private const float SlotHoverWidth = 66f;
    private const float SlotHoverHeight = 86f;

    public static void UpdateVisuals(Player? player)
    {
        if (player?.Creature is null)
            return;

        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        var visuals = creatureNode?.Visuals;
        if (creatureNode is null || visuals is null)
            return;

        var encodedOrbs = AutomatonMechanics.GetEncodedOrbs(player);
        bool shouldShow = player.Character is TheAutomaton || encodedOrbs.Count > 0;

        var tray = visuals.GetNodeOrNull<Node2D>(TrayNodeName);
        var hoverLayer = creatureNode.GetNodeOrNull<Control>(HoverLayerNodeName);

        if (!shouldShow)
        {
            if (tray is not null)
                tray.Visible = false;
            if (hoverLayer is not null)
                hoverLayer.Visible = false;
            return;
        }

        if (tray is null)
        {
            tray = CreateTrayNode();
            visuals.AddChild(tray);
        }

        if (hoverLayer is null)
        {
            hoverLayer = CreateHoverLayer(player);
            creatureNode.AddChild(hoverLayer);
        }

        tray.Visible = true;
        hoverLayer.Visible = true;
        hoverLayer.Position = visuals.Position + TrayPosition;

        if (tray.GetNodeOrNull<Label>("HeaderLabel") is { } header)
        {
            header.Text = $"FUNCTION ({encodedOrbs.Count}/{AutomatonMechanics.FunctionCapacity})";
        }

        Texture2D? pedestalTex = ResourceLoader.Exists(SequenceSlotTexturePath)
            ? ResourceLoader.Load<Texture2D>(SequenceSlotTexturePath)
            : null;
        Texture2D? fallbackTex = ResourceLoader.Exists(FallbackOrbTexturePath)
            ? ResourceLoader.Load<Texture2D>(FallbackOrbTexturePath)
            : null;

        for (int i = 0; i < AutomatonMechanics.FunctionCapacity; i++)
        {
            var slot = tray.GetNodeOrNull<Node2D>($"Slot{i}");
            var hover = hoverLayer.GetNodeOrNull<Control>($"Hover{i}");
            EncodedCardOrb? orb = i < encodedOrbs.Count ? encodedOrbs[i] : null;
            var card = orb?.EncodedCard;

            if (slot is not null)
            {
                if (slot.GetNodeOrNull<Sprite2D>("Pedestal") is { } pedestal && pedestalTex is not null)
                {
                    if (pedestal.Texture != pedestalTex)
                        pedestal.Texture = pedestalTex;
                    pedestal.Modulate = card is not null
                        ? new Color(1.12f, 1.06f, 0.90f, 1.0f)
                        : new Color(0.55f, 0.52f, 0.46f, 0.72f);
                }

                if (slot.GetNodeOrNull<Panel>("CardFrame") is { } frame)
                {
                    frame.Visible = card is not null;
                }

                if (slot.GetNodeOrNull<Sprite2D>("CardArt") is { } art)
                {
                    if (card is not null)
                    {
                        string? portraitPath = card.PortraitPath;
                        Texture2D? tex = (!string.IsNullOrEmpty(portraitPath) && ResourceLoader.Exists(portraitPath))
                            ? ResourceLoader.Load<Texture2D>(portraitPath)
                            : fallbackTex;
                        if (tex is not null)
                        {
                            art.Texture = tex;
                            Vector2 sz = tex.GetSize();
                            if (sz.X > 0f && sz.Y > 0f)
                            {
                                float sx = 54f / sz.X;
                                float sy = 42f / sz.Y;
                                float sc = Mathf.Min(sx, sy);
                                art.Scale = new Vector2(sc, sc);
                            }
                            art.Visible = true;
                        }
                        else
                        {
                            art.Visible = false;
                        }
                    }
                    else
                    {
                        art.Visible = false;
                    }
                }

                if (slot.GetNodeOrNull<Label>("SlotNumber") is { } numLabel)
                {
                    numLabel.Text = (i + 1).ToString();
                    numLabel.AddThemeColorOverride("font_color", card is not null
                        ? new Color(1.0f, 0.88f, 0.45f)
                        : new Color(0.58f, 0.56f, 0.50f));
                }
            }

            if (hover is not null)
            {
                hover.Visible = card is not null;
            }
        }
    }

    public static void PlaySlotPulse(Player? player, int slotIndex)
    {
        if (NonInteractiveMode.IsActive || player?.Creature is null || slotIndex < 0 || slotIndex >= AutomatonMechanics.FunctionCapacity)
            return;

        var visuals = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals;
        var slot = visuals?.GetNodeOrNull<Node2D>(TrayNodeName)?.GetNodeOrNull<Node2D>($"Slot{slotIndex}");
        if (slot is null || !GodotObject.IsInstanceValid(slot) || !slot.IsInsideTree())
            return;

        float speed = SaveManager.Instance?.PrefsSave.FastMode == FastModeType.Fast ? 0.65f : 1.0f;
        var tween = slot.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(slot, "scale", new Vector2(1.26f, 1.26f), 0.09 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(slot, "modulate", new Color(1.60f, 1.42f, 0.88f, 1f), 0.08 * speed);

        tween.Chain().SetParallel(true);
        tween.TweenProperty(slot, "scale", Vector2.One, 0.14 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(slot, "modulate", Colors.White, 0.14 * speed);
    }

    private static Node2D CreateTrayNode()
    {
        var root = new Node2D
        {
            Name = TrayNodeName,
            Position = TrayPosition,
            ZIndex = 6
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.07f, 0.06f, 0.84f),
            BorderColor = new Color(0.76f, 0.62f, 0.34f, 0.92f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };

        var backdrop = new Panel
        {
            Name = "Backdrop",
            Position = new Vector2(-114f, -64f),
            Size = new Vector2(228f, 120f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        backdrop.AddThemeStyleboxOverride("panel", style);
        root.AddChild(backdrop);

        var header = new Label
        {
            Name = "HeaderLabel",
            Position = new Vector2(-110f, -60f),
            Size = new Vector2(220f, 20f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "FUNCTION (0/3)",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        header.AddThemeFontSizeOverride("font_size", 13);
        header.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.52f));
        header.AddThemeColorOverride("font_outline_color", Colors.Black);
        header.AddThemeConstantOverride("outline_size", 3);
        root.AddChild(header);

        var cardFrameStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.14f, 0.12f, 0.10f, 0.95f),
            BorderColor = new Color(0.92f, 0.78f, 0.42f, 1.0f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };

        for (int i = 0; i < AutomatonMechanics.FunctionCapacity; i++)
        {
            var slot = new Node2D
            {
                Name = $"Slot{i}",
                Position = new Vector2(SlotXOffsets[i], 6f)
            };

            // Authentic StS 1 bronze sequenceSlot pedestal scaled neatly inside the tray
            var pedestal = new Sprite2D
            {
                Name = "Pedestal",
                Centered = true,
                Position = new Vector2(0f, -2f),
                Scale = new Vector2(0.46f, 0.46f)
            };
            slot.AddChild(pedestal);

            var cardFrame = new Panel
            {
                Name = "CardFrame",
                Position = new Vector2(-30f, -21f),
                Size = new Vector2(60f, 48f),
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            cardFrame.AddThemeStyleboxOverride("panel", cardFrameStyle);
            slot.AddChild(cardFrame);

            var cardArt = new Sprite2D
            {
                Name = "CardArt",
                Centered = true,
                Position = new Vector2(0f, 3f),
                Visible = false
            };
            slot.AddChild(cardArt);

            var numLabel = new Label
            {
                Name = "SlotNumber",
                Position = new Vector2(-20f, 27f),
                Size = new Vector2(40f, 18f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Text = (i + 1).ToString(),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            numLabel.AddThemeFontSizeOverride("font_size", 13);
            numLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            numLabel.AddThemeConstantOverride("outline_size", 4);
            slot.AddChild(numLabel);

            root.AddChild(slot);
        }

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

        for (int i = 0; i < AutomatonMechanics.FunctionCapacity; i++)
        {
            int slotIndex = i;
            var hover = new Control
            {
                Name = $"Hover{i}",
                Position = new Vector2(SlotXOffsets[i] - SlotHoverWidth * 0.5f, 6f - SlotHoverHeight * 0.5f),
                Size = new Vector2(SlotHoverWidth, SlotHoverHeight),
                MouseFilter = Control.MouseFilterEnum.Pass,
                Visible = false
            };

            hover.MouseEntered += () =>
            {
                var orbs = AutomatonMechanics.GetEncodedOrbs(player);
                if (slotIndex >= orbs.Count)
                    return;

                var card = orbs[slotIndex].EncodedCard;
                if (card is null)
                    return;

                NHoverTipSet.CreateAndShow(
                    hover,
                    [HoverTipFactory.FromCard(card)],
                    HoverTip.GetHoverTipAlignment(hover))?.SetFollowOwner();
            };
            hover.MouseExited += () => NHoverTipSet.Remove(hover);

            layer.AddChild(hover);
        }

        return layer;
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class AutomatonFunctionMountPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCreature __instance)
    {
        if (__instance.Entity?.Player is { Character: TheAutomaton } player)
        {
            AutomatonFunctionOverlay.UpdateVisuals(player);
        }
    }
}
