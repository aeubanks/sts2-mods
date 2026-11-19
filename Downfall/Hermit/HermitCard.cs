using System.Runtime.CompilerServices;
using BaseLib.Utils;
using Downfall.Core;
using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Combat;
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
    private static readonly ConditionalWeakTable<ICombatState, HashSet<CardPlay>> DeadOnPlaysByCombat = new();

    private bool _deadOnWhenPlayed;
    private bool _forceNextDeadOn;

    protected override string CharacterKey => TheHermit.CharKey;

    public virtual bool UsesDeadOn => false;

    internal void ForceDeadOnOnce() => _forceNextDeadOn = true;

    internal void ClearForcedDeadOn() => _forceNextDeadOn = false;

    protected bool IsDeadOnInHand()
    {
        if (!UsesDeadOn || CombatState is null || Pile?.Type != PileType.Hand)
            return false;

        if (Owner.Creature.HasPower<ConcentratedPower>())
            return true;

        var cards = Pile.Cards;
        int idx = -1;
        for (int i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], this))
            {
                idx = i;
                break;
            }
        }
        return idx >= 0 && Math.Abs(2 * idx + 1 - cards.Count) <= 1;
    }

    protected internal bool IsDeadOnNow => Pile?.Type == PileType.Play ? _deadOnWhenPlayed : IsDeadOnInHand();

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
            _deadOnWhenPlayed = _forceNextDeadOn || Owner.Creature.HasPower<ConcentratedPower>() || IsDeadOnInHand();
        }
        return true;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (UsesDeadOn && ReferenceEquals(cardPlay.Card, this) && CombatState is not null)
        {
            if (_forceNextDeadOn || Owner.Creature.HasPower<ConcentratedPower>())
            {
                _deadOnWhenPlayed = true;
            }
            _forceNextDeadOn = false;
            if (_deadOnWhenPlayed)
            {
                DeadOnPlaysByCombat.GetOrCreateValue(CombatState).Add(cardPlay);
            }
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (ReferenceEquals(card, this) && Pile?.Type != PileType.Play && Pile?.Type != PileType.Hand)
        {
            _deadOnWhenPlayed = false;
            _forceNextDeadOn = false;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && cardPlay.PlayIndex >= cardPlay.PlayCount - 1)
        {
            _deadOnWhenPlayed = false;
        }
        return Task.CompletedTask;
    }

    internal static bool WasCardPlayDeadOn(ICombatState? combatState, CardPlay cardPlay) =>
        combatState is not null
        && DeadOnPlaysByCombat.TryGetValue(combatState, out var deadOnPlays)
        && deadOnPlays.Contains(cardPlay);

    internal static bool WasLastCardPlayedThisTurnDeadOn(Player? owner, ICombatState? combatState, CardPlay? currentPlay = null)
    {
        if (owner is null
            || combatState is null
            || !DeadOnPlaysByCombat.TryGetValue(combatState, out var deadOnPlays)
            || deadOnPlays.Count == 0)
        {
            return false;
        }

        CardPlay? previous = null;
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.CardPlay.Card.Owner != owner || !entry.HappenedThisTurn(combatState))
                continue;

            if (currentPlay is not null && ReferenceEquals(entry.CardPlay, currentPlay))
                return previous is not null && deadOnPlays.Contains(previous);

            previous = entry.CardPlay;
        }

        return previous is not null && deadOnPlays.Contains(previous);
    }
}
