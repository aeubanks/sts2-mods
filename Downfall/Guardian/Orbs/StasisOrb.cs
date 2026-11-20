using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

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

        if (_turnsRemaining <= 0)
        {
            await GuardianMechanics.EvokeStasisIfHandHasRoomAsync(choiceContext, Owner, this);
        }
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
