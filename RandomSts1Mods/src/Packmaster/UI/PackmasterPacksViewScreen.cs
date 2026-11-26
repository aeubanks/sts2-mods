using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using Packmaster.Character;
using Packmaster.Packs;

namespace Packmaster.UI;

/// <summary>
/// Opens a native StS 2 NSimpleCardsViewScreen populated with the local Packmaster's
/// active Pack cards, enhanced with per-Pack filter tabs (All Packs + each active Pack).
/// Uses zero custom GodotObject subclasses so Godot --export-pack stays 100% clean.
/// </summary>
public static class PackmasterPacksViewScreen
{
    private static readonly StringName MetaKey = "PackmasterActivePacksScreen";

    public static bool IsOpen =>
        NCapstoneContainer.Instance?.CurrentCapstoneScreen is Node n
        && GodotObject.IsInstanceValid(n)
        && n.HasMeta(MetaKey);

    public static NCardsViewScreen? ShowScreen(Player player)
    {
        var activePacks = PackRunState.GetActivePackDefinitions(player);
        var poolCards = ModelDb.CardPool<PackmasterCardPool>().AllCards
            .Where(c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();

        var cardsByPack = new Dictionary<string, List<CardModel>>(StringComparer.OrdinalIgnoreCase);
        var allOrderedCards = new List<CardModel>();

        foreach (var pack in activePacks)
        {
            var packCards = poolCards
                .Where(c => ReferenceEquals(PackRegistry.GetForCard(c), pack))
                .OrderBy(GetRarityOrder)
                .ThenBy(c => c.EnergyCost.Canonical)
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            cardsByPack[pack.Id] = packCards;
            allOrderedCards.AddRange(packCards);
        }

        var addResults = allOrderedCards
            .Select(c => new CardPileAddResult
            {
                success = true,
                cardAdded = c
            })
            .ToList();

        var infoText = BuildAllPacksSubtitle(allOrderedCards.Count, activePacks.Count);
        var screen = NSimpleCardsViewScreen.ShowScreen(addResults, infoText);
        if (screen is null)
            return null;

        screen.SetMeta(MetaKey, true);
        screen.TreeExiting += PackmasterTopBarPatch.NotifyScreenClosed;

        AttachPackFilterBar(screen, activePacks, cardsByPack, allOrderedCards);
        return screen;
    }

    private static void AttachPackFilterBar(
        NCardsViewScreen screen,
        IReadOnlyList<PackDefinition> activePacks,
        Dictionary<string, List<CardModel>> cardsByPack,
        List<CardModel> allOrderedCards)
    {
        var grid = screen.GetNodeOrNull<NCardGrid>("CardGrid");
        var bottomLabel = screen.GetNodeOrNull<RichTextLabel>("%BottomLabel");
        if (grid is null)
            return;

        // Reserve room below the top bar for the Pack filter tab strip.
        grid.SetAnchorAndOffset(Side.Top, 0f, 152f);

        var barPanel = new PanelContainer
        {
            Name = "PackmasterPackFilterBar",
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        barPanel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        barPanel.OffsetTop = 82f;
        barPanel.OffsetBottom = 148f;
        barPanel.OffsetLeft = 80f;
        barPanel.OffsetRight = -80f;
        barPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        screen.AddChild(barPanel);

        var tabsFlow = new HFlowContainer
        {
            Alignment = FlowContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        tabsFlow.AddThemeConstantOverride("h_separation", 8);
        tabsFlow.AddThemeConstantOverride("v_separation", 6);
        barPanel.AddChild(tabsFlow);

        var tabButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        string? selectedPackId = null;

        void UpdateSelection(string? newPackId)
        {
            selectedPackId = newPackId;
            string activeKey = selectedPackId ?? string.Empty;
            foreach (var (key, btn) in tabButtons)
            {
                ApplyPillStyle(btn, string.Equals(key, activeKey, StringComparison.OrdinalIgnoreCase));
            }

            List<CardModel> targetCards;
            if (selectedPackId is null)
            {
                targetCards = allOrderedCards;
                if (bottomLabel is not null)
                    bottomLabel.Text = BuildAllPacksSubtitle(allOrderedCards.Count, activePacks.Count).GetFormattedText();
            }
            else
            {
                targetCards = cardsByPack.TryGetValue(selectedPackId, out var list) ? list : [];
                var pack = activePacks.FirstOrDefault(p => string.Equals(p.Id, selectedPackId, StringComparison.OrdinalIgnoreCase));
                if (bottomLabel is not null && pack is not null)
                {
                    string title = pack.Title.Exists() ? pack.Title.GetFormattedText() : pack.Id;
                    string desc = pack.Description.Exists() ? pack.Description.GetFormattedText() : string.Empty;
                    bottomLabel.Text = $"[gold]{title}[/gold] — {desc}";
                }
            }

            grid.SetCards(targetCards, PileType.None, [SortingOrders.Ascending]);
        }

        string allLabel = GetLocText("PACKMASTER-ACTIVE_PACKS_SCREEN.allPacks", "All Packs");
        var allBtn = CreateTabButton($"{allLabel} ({allOrderedCards.Count})", null, () => UpdateSelection(null));
        tabButtons[string.Empty] = allBtn;
        tabsFlow.AddChild(allBtn);

        foreach (var pack in activePacks)
        {
            int count = cardsByPack.TryGetValue(pack.Id, out var list) ? list.Count : 0;
            string title = pack.Title.Exists() ? pack.Title.GetFormattedText() : pack.Id;
            Texture2D? icon = ResourceLoader.Exists(pack.IconPath)
                ? ResourceLoader.Load<Texture2D>(pack.IconPath)
                : null;
            string capturedId = pack.Id;
            var btn = CreateTabButton($"{title} ({count})", icon, () => UpdateSelection(capturedId));
            tabButtons[pack.Id] = btn;
            tabsFlow.AddChild(btn);
        }

        foreach (var (key, btn) in tabButtons)
        {
            ApplyPillStyle(btn, string.IsNullOrEmpty(key));
        }
    }

    private static Button CreateTabButton(string label, Texture2D? icon, Action onPressed)
    {
        var btn = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(0, 36),
            ExpandIcon = false
        };
        btn.AddThemeFontSizeOverride("font_size", 16);
        if (icon is not null)
        {
            var img = icon.GetImage();
            if (img is not null)
            {
                img.Resize(22, 22, Image.Interpolation.Bilinear);
                btn.Icon = ImageTexture.CreateFromImage(img);
            }
        }
        btn.Pressed += onPressed;
        return btn;
    }

    private static void ApplyPillStyle(Button button, bool selected)
    {
        var normalBg = selected
            ? new Color(0.55f, 0.43f, 0.25f, 0.96f)
            : new Color(0.14f, 0.12f, 0.17f, 0.92f);
        var hoverBg = selected
            ? new Color(0.66f, 0.53f, 0.31f, 0.98f)
            : new Color(0.24f, 0.21f, 0.28f, 0.96f);
        var border = selected
            ? new Color(0.95f, 0.82f, 0.52f, 1.0f)
            : new Color(0.55f, 0.47f, 0.38f, 0.65f);

        button.AddThemeStyleboxOverride("normal", MakeBox(normalBg, border));
        button.AddThemeStyleboxOverride("hover", MakeBox(hoverBg, new Color(0.95f, 0.82f, 0.52f, 0.92f)));
        button.AddThemeStyleboxOverride("pressed", MakeBox(hoverBg, new Color(1f, 0.9f, 0.6f, 1f)));
        button.AddThemeStyleboxOverride("focus", MakeBox(hoverBg, new Color(1f, 0.9f, 0.6f, 1f)));
        button.AddThemeColorOverride("font_color", selected ? Colors.White : new Color(0.92f, 0.89f, 0.83f));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
    }

    private static StyleBoxFlat MakeBox(Color bg, Color border) => new()
    {
        BgColor = bg,
        BorderColor = border,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 5,
        ContentMarginBottom = 5
    };

    private static LocString BuildAllPacksSubtitle(int cardCount, int packCount)
    {
        var loc = new LocString("gameplay_ui", "PACKMASTER-ACTIVE_PACKS_SCREEN.subtitleAll");
        loc.Add("CardCount", cardCount);
        loc.Add("PackCount", packCount);
        return loc;
    }

    private static string GetLocText(string key, string fallback)
    {
        var loc = new LocString("gameplay_ui", key);
        return loc.Exists() ? loc.GetFormattedText() : fallback;
    }

    private static int GetRarityOrder(CardModel card) => card.Rarity switch
    {
        CardRarity.Common => 0,
        CardRarity.Uncommon => 1,
        CardRarity.Rare => 2,
        _ => 3
    };
}
