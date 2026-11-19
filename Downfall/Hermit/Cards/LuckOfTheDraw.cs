using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Cards;

public sealed class LuckOfTheDraw()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "luck_of_the_draw";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("CostThreshold", 3m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        const int maxDraws = 100;
        int threshold = DynamicVars["CostThreshold"].IntValue;
        int totalCost = 0;

        for (int draws = 0; totalCost < threshold && draws < maxDraws; draws++)
        {
            var drawn = (await CardPileCmd.Draw(choiceContext, 1, Owner)).ToList();
            if (drawn.Count == 0)
                break;

            foreach (var card in drawn)
            {
                if (!card.EnergyCost.CostsX && !card.Keywords.Contains(CardKeyword.Unplayable))
                {
                    int cost = card.EnergyCost.GetWithModifiers(CostModifiers.All);
                    if (cost > 0)
                    {
                        totalCost += cost;
                    }
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CostThreshold"].UpgradeValueBy(1m);
    }
}
