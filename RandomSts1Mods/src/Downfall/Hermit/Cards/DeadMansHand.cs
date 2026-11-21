using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hermit.Cards;

public sealed class DeadMansHand()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "dead_mans_hand";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    private static int RarityRank(CardModel card)
    {
        if (card.Type == CardType.Status)
            return -1;
        if (card.Type == CardType.Curse)
            return 0;

        return card.Rarity switch
        {
            CardRarity.Ancient => 6,
            CardRarity.Rare => 5,
            CardRarity.Uncommon => 4,
            CardRarity.Common => 3,
            CardRarity.Event or CardRarity.Token or CardRarity.Quest => 2,
            CardRarity.Basic => 1,
            CardRarity.Curse => 0,
            _ => -1,
        };
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var handCards = PileType.Hand.GetPile(Owner).Cards.ToList();
        if (handCards.Count > 0)
        {
            await CardCmd.Discard(choiceContext, handCards);
        }

        var drawPile = PileType.Draw.GetPile(Owner);
        var rarest = drawPile.Cards
            .OrderByDescending(RarityRank)
            .Take(DynamicVars.Cards.IntValue)
            .ToList();

        foreach (var card in rarest)
        {
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
