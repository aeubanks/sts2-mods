using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Runs;
using Packmaster.Character;
using Packmaster.Packs;

namespace Packmaster.UI;

/// <summary>
/// Adds a Packmaster Active Packs button directly beside the Master Deck button in the top bar
/// whenever the local player is playing as The Packmaster.
/// Uses a plain Godot.Control (no custom GodotObject subclass) so Godot --export-pack stays clean.
/// </summary>
[HarmonyPatch(typeof(NTopBar), nameof(NTopBar.Initialize))]
internal static class PackmasterTopBarPatch
{
    internal const string ButtonNodeName = "PackmasterActivePacksButton";
    private const string IconPath = "res://Packmaster/images/relics/pm_booster_pack.png";

    private static TopBarButtonController? _activeController;

    [HarmonyPostfix]
    private static void Postfix(NTopBar __instance, IRunState runState)
    {
        var existing = __instance.RightContainer?.GetNodeOrNull<Node>(ButtonNodeName);
        if (existing is not null)
        {
            existing.QueueFree();
            _activeController?.Detach();
            _activeController = null;
        }

        var localPlayer = LocalContext.GetMe(runState);
        if (localPlayer is null || localPlayer.Character is not ThePackmaster)
            return;

        var container = __instance.RightContainer;
        if (container is null)
            return;

        var controller = new TopBarButtonController(localPlayer);
        var buttonNode = controller.Root;
        container.AddChild(buttonNode);

        // Place directly next to the Master Deck button (just to its left).
        if (__instance.Deck is not null && __instance.Deck.GetParent() == container)
        {
            int deckIndex = __instance.Deck.GetIndex();
            container.MoveChild(buttonNode, deckIndex);
        }

        _activeController = controller;
    }

    internal static void NotifyScreenClosed()
    {
        _activeController?.RefreshVisualState();
    }

    private sealed class TopBarButtonController
    {
        private readonly Player _player;
        private readonly TextureRect _iconRect;
        private readonly Label _countLabel;
        private Tween? _scaleTween;
        private bool _isHovered;
        private bool _detached;

        public Control Root { get; }

        public TopBarButtonController(Player player)
        {
            _player = player;

            Root = new Control
            {
                Name = ButtonNodeName,
                CustomMinimumSize = new Vector2(64, 64),
                Size = new Vector2(64, 64),
                PivotOffset = new Vector2(32, 32),
                MouseFilter = Control.MouseFilterEnum.Stop,
                FocusMode = Control.FocusModeEnum.All,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };

            _iconRect = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(54, 54),
                Position = new Vector2(5, 5),
                Size = new Vector2(54, 54),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            if (ResourceLoader.Exists(IconPath))
            {
                _iconRect.Texture = ResourceLoader.Load<Texture2D>(IconPath);
            }
            Root.AddChild(_iconRect);

            _countLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Position = new Vector2(20, 34),
                Size = new Vector2(40, 26),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _countLabel.AddThemeFontSizeOverride("font_size", 20);
            _countLabel.AddThemeColorOverride("font_color", Colors.White);
            _countLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            _countLabel.AddThemeConstantOverride("outline_size", 5);
            Root.AddChild(_countLabel);

            Root.MouseEntered += OnHoverStart;
            Root.MouseExited += OnHoverEnd;
            Root.FocusEntered += OnHoverStart;
            Root.FocusExited += OnHoverEnd;
            Root.GuiInput += OnGuiInput;
            Root.TreeExiting += Detach;

            PackRunState.ActivePacksChanged += OnActivePacksChanged;
            RefreshVisualState();
        }

        public void Detach()
        {
            if (_detached)
                return;
            _detached = true;
            PackRunState.ActivePacksChanged -= OnActivePacksChanged;
            if (GodotObject.IsInstanceValid(Root))
                NHoverTipSet.Remove(Root);
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Root.AcceptEvent();
                TogglePacksScreen();
            }
        }

        private void OnActivePacksChanged(IRunState state, Player? changedPlayer)
        {
            if (_detached || !GodotObject.IsInstanceValid(Root))
                return;
            if (!ReferenceEquals(state, _player.RunState))
                return;
            if (changedPlayer is not null && changedPlayer.NetId != _player.NetId)
                return;

            RefreshVisualState();
        }

        public void RefreshVisualState()
        {
            if (_detached || !GodotObject.IsInstanceValid(Root))
                return;

            var activePacks = PackRunState.GetActivePackDefinitions(_player);
            _countLabel.Text = activePacks.Count.ToString();

            bool isOpen = PackmasterPacksViewScreen.IsOpen;
            _iconRect.Modulate = isOpen
                ? new Color(1.15f, 1.05f, 0.85f, 1f)
                : _isHovered ? new Color(1.1f, 1.1f, 1.1f, 1f) : Colors.White;
        }

        private void TogglePacksScreen()
        {
            NHoverTipSet.Remove(Root);

            var capstone = NCapstoneContainer.Instance;
            if (capstone is null)
                return;

            if (PackmasterPacksViewScreen.IsOpen)
            {
                capstone.Close();
            }
            else
            {
                PackmasterPacksViewScreen.ShowScreen(_player);
            }

            RefreshVisualState();
        }

        private void OnHoverStart()
        {
            _isHovered = true;
            AnimateScale(1.12f);
            RefreshVisualState();
            ShowHoverTip();
        }

        private void OnHoverEnd()
        {
            _isHovered = false;
            AnimateScale(1.0f);
            RefreshVisualState();
            NHoverTipSet.Remove(Root);
        }

        private void AnimateScale(float target)
        {
            if (!GodotObject.IsInstanceValid(Root))
                return;
            _scaleTween?.Kill();
            _scaleTween = Root.CreateTween();
            _scaleTween.TweenProperty(Root, "scale", Vector2.One * target, 0.12)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
        }

        private void ShowHoverTip()
        {
            var activePacks = PackRunState.GetActivePackDefinitions(_player);
            string activeList = activePacks.Count > 0
                ? string.Join(", ", activePacks.Select(p => p.Title.Exists() ? p.Title.GetFormattedText() : p.Id))
                : "None";

            var title = new LocString("gameplay_ui", "PACKMASTER-ACTIVE_PACKS_BUTTON.title");
            var desc = new LocString("gameplay_ui", "PACKMASTER-ACTIVE_PACKS_BUTTON.description");
            desc.Add("ActiveList", activeList);

            var tip = new HoverTip(title, desc);
            var tipSet = NHoverTipSet.CreateAndShow(Root, tip);
            if (tipSet is not null)
            {
                tipSet.SetGlobalPosition(Root.GlobalPosition + new Vector2(Root.Size.X - tipSet.Size.X, Root.Size.Y + 20f));
            }
        }
    }
}
