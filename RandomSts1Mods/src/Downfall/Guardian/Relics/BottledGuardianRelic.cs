using MegaCrit.Sts2.Core.CardSelection;
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

namespace Downfall.Guardian.Relics;

public abstract class BottledGuardianRelic : GuardianRelic
{
    private int _bottledDeckIndex = -1;
    private CardModel? _bottledDeckCard;

    public override bool HasUponPickupEffect => true;

    protected abstract string SelectionPromptKey { get; }

    [SavedProperty]
    public int BottledDeckIndex
    {
        get => _bottledDeckIndex;
        set
        {
            AssertMutable();
            _bottledDeckIndex = value;
            _bottledDeckCard = null;
        }
    }

    protected static int IndexOfCard(IReadOnlyList<CardModel> cards, CardModel target)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], target))
                return i;
        }
        return -1;
    }

    protected bool IsAlreadyBottledElsewhere(CardModel card) =>
        Owner is not null && Owner.Relics
            .OfType<BottledGuardianRelic>()
            .Any(r => !ReferenceEquals(r, this) && ReferenceEquals(r.ResolveBottledDeckCard(), card));

    protected override bool IsAllowedInternal(IRunState runState) =>
        runState.Players.Any(p => p.Deck.Cards.Count > 0);

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _bottledDeckCard = null;
    }

    protected CardModel? ResolveBottledDeckCard()
    {
        if (Owner is null)
            return null;

        if (_bottledDeckCard is not null && IndexOfCard(Owner.Deck.Cards, _bottledDeckCard) >= 0)
            return _bottledDeckCard;

        if (_bottledDeckIndex >= 0 && _bottledDeckIndex < Owner.Deck.Cards.Count)
        {
            _bottledDeckCard = Owner.Deck.Cards[_bottledDeckIndex];
            return _bottledDeckCard;
        }

        _bottledDeckCard = null;
        return null;
    }

    protected void SyncStatus()
    {
        Status = ResolveBottledDeckCard() is null ? RelicStatus.Disabled : RelicStatus.Normal;
    }

    protected bool IsBottledCombatCard(CardModel card)
    {
        var deckCard = ResolveBottledDeckCard();
        if (deckCard is null)
            return false;

        return ReferenceEquals(card, deckCard) || ReferenceEquals(card.DeckVersion, deckCard);
    }

    protected IEnumerable<IHoverTip> GetBottledCardHoverTips()
    {
        if (IsMutable && Owner is not null && ResolveBottledDeckCard() is { } card)
            yield return HoverTipFactory.FromCard(card);
    }

    public override async Task AfterObtained()
    {
        var candidates = Owner.Deck.Cards.Where(c => !IsAlreadyBottledElsewhere(c)).ToList();
        if (candidates.Count == 0)
            candidates = Owner.Deck.Cards.ToList();

        if (candidates.Count == 0)
        {
            _bottledDeckCard = null;
            _bottledDeckIndex = -1;
            Status = RelicStatus.Disabled;
            return;
        }

        var prompt = new LocString("relics", SelectionPromptKey);
        var chosen = (await CardSelectCmd.FromSimpleGrid(
            new BlockingPlayerChoiceContext(),
            candidates,
            Owner,
            new CardSelectorPrefs(prompt, 1))).FirstOrDefault();

        if (chosen is null)
        {
            _bottledDeckCard = null;
            _bottledDeckIndex = -1;
            Status = RelicStatus.Disabled;
            return;
        }

        _bottledDeckCard = chosen;
        _bottledDeckIndex = IndexOfCard(Owner.Deck.Cards, chosen);
        Status = _bottledDeckIndex >= 0 ? RelicStatus.Normal : RelicStatus.Disabled;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        SyncStatus();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        SyncStatus();
        return Task.CompletedTask;
    }

    public override Task BeforeCardRemoved(CardModel card)
    {
        if (card.Owner != Owner || card.Pile?.Type != PileType.Deck)
            return Task.CompletedTask;

        var bottled = ResolveBottledDeckCard();
        if (bottled is null)
        {
            Status = RelicStatus.Disabled;
            return Task.CompletedTask;
        }

        if (ReferenceEquals(card, bottled))
        {
            _bottledDeckCard = null;
            _bottledDeckIndex = -1;
            Status = RelicStatus.Disabled;
            return Task.CompletedTask;
        }

        int removedIndex = IndexOfCard(Owner.Deck.Cards, card);
        if (removedIndex >= 0 && removedIndex < _bottledDeckIndex)
        {
            _bottledDeckIndex--;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (oldPileType == PileType.Deck && card.Owner == Owner && _bottledDeckIndex >= 0)
        {
            if (_bottledDeckCard is null)
                return Task.CompletedTask;

            int idx = IndexOfCard(Owner.Deck.Cards, _bottledDeckCard);
            if (idx < 0)
            {
                _bottledDeckCard = null;
                _bottledDeckIndex = -1;
                Status = RelicStatus.Disabled;
            }
            else
            {
                _bottledDeckIndex = idx;
            }
        }
        return Task.CompletedTask;
    }
}
