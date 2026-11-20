using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Slimebound.Cards;

public sealed class SlimeBrawl() : SlimeCard(3, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int count = 1 + SlimeMechanics.GetSlimes(Owner).Count;
        await CardPileCmd.AutoPlayFromDrawPile(choiceContext, Owner, count, CardPilePosition.Top, forceExhaust: false);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
