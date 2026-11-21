using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Awakened.Relics;

public sealed class MoonTalisman : AwakenedRelic
{
    private int _blessedDeckIndex = -1;
    private CardModel? _blessedDeckCard;

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool HasUponPickupEffect => true;

    [SavedProperty]
    public int BlessedDeckIndex
    {
        get => _blessedDeckIndex;
        set
        {
            AssertMutable();
            _blessedDeckIndex = value;
            _blessedDeckCard = null;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            yield return AwakenedCard.ConjureHoverTip;
            if (IsMutable && Owner is not null && ResolveBlessedDeckCard() is { } blessed)
            {
                yield return HoverTipFactory.FromCard(blessed);
            }
        }
    }

    private static bool IsEligibleCard(CardModel card) =>
        card is not AwakenedCard { UsesConjure: true }
        && !card.Keywords.Contains(CardKeyword.Unplayable);

    private static int IndexOfCard(IReadOnlyList<CardModel> cards, CardModel target)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], target))
                return i;
        }
        return -1;
    }

    protected override bool IsAllowedInternal(IRunState runState) =>
        runState.Players.Any(p => p.Deck.Cards.Any(IsEligibleCard));

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _blessedDeckCard = null;
    }

    private CardModel? ResolveBlessedDeckCard()
    {
        if (_blessedDeckCard is not null && IndexOfCard(Owner.Deck.Cards, _blessedDeckCard) >= 0)
            return _blessedDeckCard;

        if (_blessedDeckIndex >= 0 && _blessedDeckIndex < Owner.Deck.Cards.Count)
        {
            _blessedDeckCard = Owner.Deck.Cards[_blessedDeckIndex];
            return _blessedDeckCard;
        }

        _blessedDeckCard = null;
        return null;
    }

    private void SyncBlessedCardAndStatus()
    {
        var blessed = ResolveBlessedDeckCard();
        Status = blessed is null ? RelicStatus.Disabled : RelicStatus.Normal;
    }

    internal bool IsBlessedCard(CardModel card)
    {
        var deckCard = ResolveBlessedDeckCard();
        if (deckCard is null)
            return false;

        return ReferenceEquals(card, deckCard) || ReferenceEquals(card.DeckVersion, deckCard);
    }

    public override async Task AfterObtained()
    {
        var candidates = Owner.Deck.Cards
            .Where(IsEligibleCard)
            .ToList();

        if (candidates.Count == 0)
        {
            _blessedDeckCard = null;
            _blessedDeckIndex = -1;
            Status = RelicStatus.Disabled;
            return;
        }

        var prompt = new LocString("relics", "DOWNFALL-MOON_TALISMAN.selectionScreenPrompt");
        var prefs = new CardSelectorPrefs(prompt, 1);
        var chosen = (await CardSelectCmd.FromSimpleGrid(
            new BlockingPlayerChoiceContext(),
            candidates,
            Owner,
            prefs)).FirstOrDefault();

        if (chosen is null)
        {
            _blessedDeckCard = null;
            _blessedDeckIndex = -1;
            Status = RelicStatus.Disabled;
            return;
        }

        _blessedDeckCard = chosen;
        _blessedDeckIndex = IndexOfCard(Owner.Deck.Cards, chosen);
        Status = _blessedDeckIndex >= 0 ? RelicStatus.Normal : RelicStatus.Disabled;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        SyncBlessedCardAndStatus();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        SyncBlessedCardAndStatus();
        return Task.CompletedTask;
    }

    public override Task BeforeCardRemoved(CardModel card)
    {
        if (card.Owner != Owner || card.Pile?.Type != PileType.Deck)
            return Task.CompletedTask;

        var blessed = ResolveBlessedDeckCard();
        if (blessed is null)
        {
            Status = RelicStatus.Disabled;
            return Task.CompletedTask;
        }

        if (ReferenceEquals(card, blessed))
        {
            _blessedDeckCard = null;
            _blessedDeckIndex = -1;
            Status = RelicStatus.Disabled;
            return Task.CompletedTask;
        }

        int removedIndex = IndexOfCard(Owner.Deck.Cards, card);
        if (removedIndex >= 0 && removedIndex < _blessedDeckIndex)
        {
            _blessedDeckIndex--;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (oldPileType == PileType.Deck && card.Owner == Owner && _blessedDeckIndex >= 0)
        {
            int idx = _blessedDeckCard is not null ? IndexOfCard(Owner.Deck.Cards, _blessedDeckCard) : -1;
            if (idx < 0)
            {
                _blessedDeckCard = null;
                _blessedDeckIndex = -1;
                Status = RelicStatus.Disabled;
            }
            else
            {
                _blessedDeckIndex = idx;
            }
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);

        if (cardPlay.Card.Owner != Owner
            || !IsBlessedCard(cardPlay.Card)
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await AwakenedSpellbook.ConjureAsync(Owner, combatState);
    }
}
