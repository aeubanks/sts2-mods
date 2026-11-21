using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Gremlins.Cards;

public sealed class SharpenBlades()
    : GremlinCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        foreach (var card in PileType.Hand.GetPile(Owner).Cards)
        {
            if (card.Type == CardType.Attack && !card.EnergyCost.CostsX)
            {
                card.SetToFreeThisTurn();
            }
        }
    }
}
