using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace Downfall.Automaton.Orbs;

/// <summary>
/// Represents a card currently Encoded in The Automaton's 3-slot Function sequence.
/// Displays a single preloaded bronze sequence node icon with the slot number (1..3)
/// and shows the full encoded card on hover.
/// </summary>
public sealed class EncodedCardOrb : DownfallOrb
{
    private BronzeCard? _encodedCard;
    private int _sequenceSlotIndex;

    protected override string CharacterKey => TheAutomaton.CharKey;
    public override Color DarkenedColor => new("#7A6A42");
    public override bool ShowSecondaryLabel => false;
    public override string EvokePreviewText => string.Empty;

    public BronzeCard? EncodedCard => _encodedCard;

    public int SequenceSlotIndex
    {
        get => _sequenceSlotIndex;
        internal set
        {
            AssertMutable();
            _sequenceSlotIndex = Math.Max(0, value);
            if (_encodedCard is not null)
                _encodedCard.SequenceSlotIndex = _sequenceSlotIndex;
        }
    }

    public override decimal PassiveVal => _sequenceSlotIndex + 1;
    public override decimal EvokeVal => _sequenceSlotIndex + 1;
    public override string PrimaryLabelText => (_sequenceSlotIndex + 1).ToString();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        _encodedCard is not null
            ? [HoverTipFactory.FromCard(_encodedCard)]
            : [];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _encodedCard = null;
        _sequenceSlotIndex = 0;
    }

    internal void InitializeEncodedCard(BronzeCard card, int slotIndex)
    {
        AssertMutable();
        _encodedCard = card;
        _sequenceSlotIndex = Math.Max(0, slotIndex);
        card.SequenceSlotIndex = _sequenceSlotIndex;
    }

    internal void PlayPulseVisual() => RequestAnimation("pulse");

    public override Tween? PlayCustomAnimation(Sprite2D sprite, string animName)
    {
        if (NonInteractiveMode.IsActive || animName != "pulse")
            return null;

        float speed = SaveManager.Instance?.PrefsSave.FastMode == FastModeType.Fast ? 0.65f : 1.0f;
        Vector2 baseScale = SpriteScale;
        sprite.Position = Vector2.Zero;
        sprite.Rotation = 0f;

        var tween = sprite.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "scale", baseScale * 1.28f, 0.09 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", new Color(1.60f, 1.42f, 0.88f, 1f), 0.08 * speed);

        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "scale", baseScale, 0.14 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "modulate", SpriteTint, 0.14 * speed);
        return tween;
    }

    public override Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext playerChoiceContext)
    {
        // Encoded cards are consumed by CompileFunctionAsync (or explicitly extracted by BitShift/ByteShift/Decompile).
        return Task.FromResult<IEnumerable<Creature>>([]);
    }
}
