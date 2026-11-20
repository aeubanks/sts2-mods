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

namespace Downfall.Champ.Relics;

public sealed class SignatureFinisher : ChampRelic
{
    private int _signatureDeckIndex = -1;
    private CardModel? _signatureDeckCard;

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool HasUponPickupEffect => true;

    [SavedProperty]
    public int SignatureDeckIndex
    {
        get => _signatureDeckIndex;
        set
        {
            AssertMutable();
            _signatureDeckIndex = value;
            _signatureDeckCard = null;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            yield return ChampCard.FinisherHoverTip;
            if (IsMutable && Owner is not null && ResolveSignatureDeckCard() is { } card)
            {
                yield return HoverTipFactory.FromCard(card);
            }
        }
    }

    private static bool IsEligibleCard(CardModel card) =>
        card is ChampCard { IsFinisher: true };

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
        _signatureDeckCard = null;
    }

    private CardModel? ResolveSignatureDeckCard()
    {
        if (_signatureDeckCard is not null && IndexOfCard(Owner.Deck.Cards, _signatureDeckCard) >= 0)
            return _signatureDeckCard;

        if (_signatureDeckIndex >= 0 && _signatureDeckIndex < Owner.Deck.Cards.Count)
        {
            _signatureDeckCard = Owner.Deck.Cards[_signatureDeckIndex];
            return _signatureDeckCard;
        }

        _signatureDeckCard = null;
        return null;
    }

    private void SyncSignatureAndStatus()
    {
        Status = ResolveSignatureDeckCard() is null ? RelicStatus.Disabled : RelicStatus.Normal;
    }

    internal bool IsSignatureCard(CardModel card)
    {
        var deckCard = ResolveSignatureDeckCard();
        if (deckCard is null)
            return false;

        return ReferenceEquals(card, deckCard) || ReferenceEquals(card.DeckVersion, deckCard);
    }

    internal void FlashRelic() => Flash();

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card.Owner == Owner && IsSignatureCard(card) && !card.EnergyCost.CostsX)
        {
            modifiedCost = 0m;
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    public override async Task AfterObtained()
    {
        var candidates = Owner.Deck.Cards.Where(IsEligibleCard).ToList();
        if (candidates.Count == 0)
        {
            _signatureDeckCard = null;
            _signatureDeckIndex = -1;
            Status = RelicStatus.Disabled;
            return;
        }

        var prompt = new LocString("relics", "DOWNFALL-SIGNATURE_FINISHER.selectionScreenPrompt");
        var chosen = (await CardSelectCmd.FromSimpleGrid(
            new BlockingPlayerChoiceContext(),
            candidates,
            Owner,
            new CardSelectorPrefs(prompt, 1))).FirstOrDefault();

        if (chosen is null)
        {
            _signatureDeckCard = null;
            _signatureDeckIndex = -1;
            Status = RelicStatus.Disabled;
            return;
        }

        _signatureDeckCard = chosen;
        _signatureDeckIndex = IndexOfCard(Owner.Deck.Cards, chosen);
        Status = _signatureDeckIndex >= 0 ? RelicStatus.Normal : RelicStatus.Disabled;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        SyncSignatureAndStatus();
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        SyncSignatureAndStatus();
        return Task.CompletedTask;
    }

    public override Task BeforeCardRemoved(CardModel card)
    {
        if (card.Owner != Owner || card.Pile?.Type != PileType.Deck)
            return Task.CompletedTask;

        var sig = ResolveSignatureDeckCard();
        if (sig is null)
        {
            Status = RelicStatus.Disabled;
            return Task.CompletedTask;
        }

        if (ReferenceEquals(card, sig))
        {
            _signatureDeckCard = null;
            _signatureDeckIndex = -1;
            Status = RelicStatus.Disabled;
            return Task.CompletedTask;
        }

        int removedIndex = IndexOfCard(Owner.Deck.Cards, card);
        if (removedIndex >= 0 && removedIndex < _signatureDeckIndex)
        {
            _signatureDeckIndex--;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
    {
        if (oldPileType == PileType.Deck && card.Owner == Owner && _signatureDeckIndex >= 0)
        {
            int idx = _signatureDeckCard is not null ? IndexOfCard(Owner.Deck.Cards, _signatureDeckCard) : -1;
            if (idx < 0)
            {
                _signatureDeckCard = null;
                _signatureDeckIndex = -1;
                Status = RelicStatus.Disabled;
            }
            else
            {
                _signatureDeckIndex = idx;
            }
        }
        return Task.CompletedTask;
    }
}
