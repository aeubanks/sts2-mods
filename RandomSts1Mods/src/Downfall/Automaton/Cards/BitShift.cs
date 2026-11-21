using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Bit Shift (Common, 0 cost, Exhaust; upgraded gains Retain):
/// Choose an Encoded card to return to your Hand. It gains Retain.
/// </summary>
public sealed class BitShift() : BronzeCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    protected override bool IsPlayable =>
        !IsMutable || Owner is null || !AutomatonMechanics.IsSequenceEmpty(Owner);

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var orbs = AutomatonMechanics.GetEncodedOrbs(owner);
        var encodedCards = orbs
            .Where(o => o.EncodedCard is not null)
            .Select(o => (CardModel)o.EncodedCard!)
            .ToList();
        if (encodedCards.Count == 0)
            return;

        CardModel? chosen;
        if (encodedCards.Count == 1)
        {
            chosen = encodedCards[0];
        }
        else
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, encodedCards, owner, prefs)).FirstOrDefault();
        }

        if (chosen is not BronzeCard chosenBronze || !owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var orb = AutomatonMechanics.GetEncodedOrbs(owner).FirstOrDefault(o => ReferenceEquals(o.EncodedCard, chosenBronze));
        if (orb is null)
            return;

        AutomatonMechanics.RemoveEncodedOrb(owner, orb);
        chosenBronze.SequenceSlotIndex = -1;
        chosenBronze.AddKeyword(CardKeyword.Retain);

        var hand = PileType.Hand.GetPile(owner);
        var dest = hand.Cards.Count < CardPile.MaxCardsInHand ? PileType.Hand : PileType.Discard;
        await CardPileCmd.Add(chosenBronze, dest);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
