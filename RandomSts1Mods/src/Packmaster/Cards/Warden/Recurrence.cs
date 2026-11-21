using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Packmaster.Cards.Warden;

public sealed class Recurrence()
    : PackmasterCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "recurrence";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = PileType.Hand.GetPile(Owner);
        var clones = hand.Cards
            .Where(c => !ReferenceEquals(c, this))
            .Select(c => c.CreateClone())
            .ToList();

        if (clones.Count > 0)
        {
            await CardPileCmd.AddGeneratedCardsToCombat(clones, PileType.Draw, Owner, CardPilePosition.Random);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
