using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Cards;

public sealed class Gambit()
    : HermitCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "gambit";

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = PileType.Hand.GetPile(Owner);
        var attacks = PileType.Discard.GetPile(Owner).Cards
            .Where(c => c.Type == CardType.Attack)
            .ToList();

        int count = DynamicVars.Cards.IntValue;
        for (int i = 0; i < count && attacks.Count > 0 && hand.Cards.Count < CardPile.MaxCardsInHand; i++)
        {
            var chosen = Owner.RunState.Rng.CombatCardSelection.NextItem(attacks);
            if (chosen is null)
                break;

            attacks.Remove(chosen);
            await CardPileCmd.Add(chosen, PileType.Hand);
            if (!chosen.EnergyCost.CostsX && chosen.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            {
                chosen.EnergyCost.AddThisTurn(-1, reduceOnly: true);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
