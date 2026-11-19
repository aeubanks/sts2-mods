using System.Runtime.CompilerServices;
using BaseLib.Utils;
using Downfall.Core;
using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hermit;

[Pool(typeof(HermitCardPool))]
public abstract class HermitCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    private sealed class DeadOnCombatTracker
    {
        public Dictionary<CardPlay, int> TriggerCounts { get; } = [];
        public Dictionary<Player, int> TriggersThisTurn { get; } = [];
        public int TurnRound { get; set; } = -1;

        public void EnsureTurn(int roundNumber)
        {
            if (TurnRound != roundNumber)
            {
                TurnRound = roundNumber;
                TriggersThisTurn.Clear();
            }
        }

        public void RecordTriggers(int roundNumber, Player owner, CardPlay cardPlay, int count)
        {
            EnsureTurn(roundNumber);
            TriggerCounts[cardPlay] = TriggerCounts.GetValueOrDefault(cardPlay) + count;
            TriggersThisTurn[owner] = TriggersThisTurn.GetValueOrDefault(owner) + count;
        }
    }

    private static readonly ConditionalWeakTable<ICombatState, DeadOnCombatTracker> DeadOnTrackers = new();

    private bool _deadOnWhenPlayed;
    private int _forcedDeadOnTriggers;
    private int _capturedMinTriggers = 1;

    protected override string CharacterKey => TheHermit.CharKey;

    public virtual bool UsesDeadOn => false;

    internal void ForceDeadOnOnce(int minTriggers = 1) =>
        _forcedDeadOnTriggers = Math.Max(_forcedDeadOnTriggers, minTriggers);

    internal void ClearForcedDeadOn() => _forcedDeadOnTriggers = 0;

    internal static bool IsCardInDeadOnPosition(CardModel card)
    {
        if (card.CombatState is null || card.Pile?.Type != PileType.Hand)
            return false;

        var cards = card.Pile.Cards;
        int idx = -1;
        for (int i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], card))
            {
                idx = i;
                break;
            }
        }
        return idx >= 0 && Math.Abs(2 * idx + 1 - cards.Count) <= 1;
    }

    protected bool IsDeadOnInHand()
    {
        if (!UsesDeadOn || CombatState is null || Pile?.Type != PileType.Hand)
            return false;

        return Owner.Creature.HasPower<ConcentratedPower>() || IsCardInDeadOnPosition(this);
    }

    protected internal bool IsDeadOnNow => Pile?.Type == PileType.Play ? _deadOnWhenPlayed : IsDeadOnInHand();

    protected int GetPendingDeadOnTriggerCount()
    {
        if (!IsDeadOnNow || CombatState is null)
            return 0;

        int baseTriggers = Pile?.Type == PileType.Play ? Math.Max(1, _capturedMinTriggers) : 1;
        bool hasSnipe = Owner.Creature.GetPower<SnipePower>() is { Amount: > 0 };
        return baseTriggers + (hasSnipe ? 1 : 0);
    }

    protected async Task<int> ConsumeDeadOnTriggersAsync(CardPlay cardPlay)
    {
        if (!IsDeadOnNow || CombatState is null)
            return 0;

        int triggers = Math.Max(1, _capturedMinTriggers);
        if (Owner.Creature.GetPower<SnipePower>() is { Amount: > 0 } snipe)
        {
            snipe.TriggerFlash();
            await PowerCmd.Decrement(snipe);
            triggers++;
        }

        DeadOnTrackers.GetOrCreateValue(CombatState).RecordTriggers(CombatState.RoundNumber, Owner, cardPlay, triggers);
        foreach (var blackPowder in Owner.Relics.OfType<Relics.BlackPowder>())
        {
            await blackPowder.OnDeadOnTriggeredAsync(triggers);
        }
        return triggers;
    }

    protected override bool ShouldGlowGoldInternal => UsesDeadOn && IsDeadOnInHand();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        UsesDeadOn ? [DeadOnHoverTip] : [];

    internal static IHoverTip DeadOnHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-DEAD_ON_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-DEAD_ON_KEYWORD.description"));

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        if (UsesDeadOn && ReferenceEquals(card, this) && Pile?.Type != PileType.Play && CombatState is not null)
        {
            _deadOnWhenPlayed = _forcedDeadOnTriggers > 0 || Owner.Creature.HasPower<ConcentratedPower>() || IsDeadOnInHand();
            _capturedMinTriggers = Math.Max(1, _forcedDeadOnTriggers);
        }
        return true;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (UsesDeadOn && ReferenceEquals(cardPlay.Card, this) && CombatState is not null)
        {
            if (_forcedDeadOnTriggers > 0)
            {
                _deadOnWhenPlayed = true;
                _capturedMinTriggers = _forcedDeadOnTriggers;
            }
            else if (Owner.Creature.HasPower<ConcentratedPower>())
            {
                _deadOnWhenPlayed = true;
            }
            _forcedDeadOnTriggers = 0;

            if (_deadOnWhenPlayed)
            {
                DeadOnTrackers.GetOrCreateValue(CombatState).TriggerCounts.TryAdd(cardPlay, 0);
            }
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (ReferenceEquals(card, this) && Pile?.Type != PileType.Play && Pile?.Type != PileType.Hand)
        {
            _deadOnWhenPlayed = false;
            _forcedDeadOnTriggers = 0;
            _capturedMinTriggers = 1;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && cardPlay.PlayIndex >= cardPlay.PlayCount - 1)
        {
            _deadOnWhenPlayed = false;
            _capturedMinTriggers = 1;
        }
        return Task.CompletedTask;
    }

    internal static int GetDeadOnTriggerCount(ICombatState? combatState, CardPlay cardPlay)
    {
        if (combatState is null || !DeadOnTrackers.TryGetValue(combatState, out var tracker))
            return 0;

        return tracker.TriggerCounts.TryGetValue(cardPlay, out int count) ? Math.Max(1, count) : 0;
    }

    internal static int GetDeadOnTriggersThisTurn(Player? owner, ICombatState? combatState)
    {
        if (owner is null || combatState is null || !DeadOnTrackers.TryGetValue(combatState, out var tracker))
            return 0;

        tracker.EnsureTurn(combatState.RoundNumber);
        return tracker.TriggersThisTurn.GetValueOrDefault(owner);
    }

    internal static bool WasLastCardPlayedThisTurnDeadOn(Player? owner, ICombatState? combatState, CardPlay? currentPlay = null)
    {
        if (owner is null
            || combatState is null
            || !DeadOnTrackers.TryGetValue(combatState, out var tracker)
            || tracker.TriggerCounts.Count == 0)
        {
            return false;
        }

        CardPlay? previous = null;
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.CardPlay.Card.Owner != owner || !entry.HappenedThisTurn(combatState))
                continue;

            if (currentPlay is not null && ReferenceEquals(entry.CardPlay, currentPlay))
                return previous is not null && tracker.TriggerCounts.ContainsKey(previous);

            previous = entry.CardPlay;
        }

        return previous is not null && tracker.TriggerCounts.ContainsKey(previous);
    }
}
