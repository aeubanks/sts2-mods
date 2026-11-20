using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace Downfall.Guardian.Orbs;

public sealed class StasisOrb : DownfallOrb
{
    private CardModel? _stasisCard;
    private int _turnsRemaining = 1;
    private bool _placedBeforeTurnOne;
    private bool _skipEvokeCardReturn;
    private bool _cardReleased;

    protected override string CharacterKey => TheGuardian.CharKey;
    public override Color DarkenedColor => new("#2E6D7E");
    public override bool ShowSecondaryLabel => false;
    public override string EvokePreviewText => string.Empty;

    public CardModel? StasisCard => _stasisCard;
    public int TurnsRemaining
    {
        get => _turnsRemaining;
        set
        {
            AssertMutable();
            _turnsRemaining = Math.Max(0, value);
        }
    }

    public bool PlacedBeforeTurnOne
    {
        get => _placedBeforeTurnOne;
        set
        {
            AssertMutable();
            _placedBeforeTurnOne = value;
        }
    }

    public bool SkipEvokeCardReturn
    {
        get => _skipEvokeCardReturn;
        set
        {
            AssertMutable();
            _skipEvokeCardReturn = value;
        }
    }

    public override decimal PassiveVal => _turnsRemaining;
    public override decimal EvokeVal => _turnsRemaining;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        _stasisCard is not null
            ? [HoverTipFactory.FromCard(_stasisCard)]
            : [];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _stasisCard = null;
        _turnsRemaining = 1;
        _placedBeforeTurnOne = false;
        _skipEvokeCardReturn = false;
        _cardReleased = false;
    }

    internal void InitializeCard(CardModel card, bool isSelfStasis, bool placedBeforeTurnOne)
    {
        AssertMutable();
        _stasisCard = card;
        _placedBeforeTurnOne = placedBeforeTurnOne;
        _cardReleased = false;

        int cost = card.EnergyCost.CostsX
            ? 0
            : (isSelfStasis
                ? card.EnergyCost.GetWithModifiers(CostModifiers.None)
                : card.EnergyCost.GetWithModifiers(CostModifiers.All));

        _turnsRemaining = Math.Max(1, cost + 1);
    }

    public override async Task AfterTurnStartOrbTrigger(PlayerChoiceContext choiceContext)
    {
        if (Owner is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (_placedBeforeTurnOne && Owner.Creature.CombatState?.RoundNumber <= 1)
            return;

        await TickTurnAsync(choiceContext);
    }

    internal async Task TickTurnAsync(PlayerChoiceContext choiceContext)
    {
        if (Owner is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (_stasisCard is IInStasisCard inStasis)
            await inStasis.OnTurnStartInStasisAsync(choiceContext, this);

        if (!Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.PlayerCombatState?.OrbQueue.Orbs.Contains(this) != true)
        {
            return;
        }

        if (_turnsRemaining > 0)
            _turnsRemaining--;

        GuardianMechanics.RefreshOrbVisuals(Owner);
        RequestAnimation("tick");

        if (_turnsRemaining <= 0)
        {
            await Cmd.CustomScaledWait(0.06f, 0.12f);
            await GuardianMechanics.EvokeStasisIfHandHasRoomAsync(choiceContext, Owner, this);
        }
    }

    public override Tween? PlayCustomAnimation(Sprite2D sprite, string animName)
    {
        if (NonInteractiveMode.IsActive || animName != "tick")
            return null;

        float speed = SaveManager.Instance?.PrefsSave.FastMode == FastModeType.Fast ? 0.65f : 1.0f;
        Vector2 baseScale = SpriteScale;
        sprite.Position = Vector2.Zero;
        sprite.Rotation = 0f;
        sprite.Scale = baseScale;

        var tween = sprite.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(sprite, "scale", baseScale * 1.24f, 0.08 * speed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", 0.14f, 0.06 * speed);
        tween.TweenProperty(sprite, "modulate", new Color(0.85f, 1.50f, 1.65f, 1f), 0.08 * speed);

        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "rotation", -0.10f, 0.07 * speed);

        tween.Chain().SetParallel(true);
        tween.TweenProperty(sprite, "scale", baseScale, 0.13 * speed)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(sprite, "rotation", 0f, 0.13 * speed);
        tween.TweenProperty(sprite, "modulate", SpriteTint, 0.13 * speed);
        return tween;
    }

    public override async Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext playerChoiceContext)
    {
        PlayEvokeSfx();
        ActivateEvoke([Owner.Creature]);

        if (_skipEvokeCardReturn || _cardReleased || _stasisCard is null || Owner.PlayerCombatState is not { } pcs)
            return [Owner.Creature];

        _cardReleased = true;
        var card = _stasisCard;

        if (card is IInStasisCard inStasis)
            await inStasis.OnEvokeFromStasisAsync(playerChoiceContext, this);

        bool isVolatile = card is GuardianCard gc && gc.IsVolatile;
        if (isVolatile)
        {
            await CardCmd.Exhaust(playerChoiceContext, card);
        }
        else
        {
            if (_turnsRemaining <= 0
                && !card.EnergyCost.CostsX
                && card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            {
                card.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
            }

            var dest = pcs.Hand.Cards.Count >= 10 ? PileType.Discard : PileType.Hand;
            await CardPileCmd.Add(card, dest);
        }

        return [Owner.Creature];
    }
}
