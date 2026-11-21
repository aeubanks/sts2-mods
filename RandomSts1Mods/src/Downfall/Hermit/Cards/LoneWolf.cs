using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hermit.Cards;

public sealed class LoneWolf()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "lone_wolf";

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = PileType.Hand.GetPile(Owner);
        var candidates = hand.Cards.Where(c => !ReferenceEquals(c, this)).ToList();
        if (candidates.Count == 0)
            return;

        CardModel? kept;
        if (candidates.Count == 1)
        {
            kept = candidates[0];
        }
        else
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            kept = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, c => !ReferenceEquals(c, this), this)).FirstOrDefault();
            if (kept is null)
                return;
        }

        if (!kept.EnergyCost.CostsX && kept.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
        {
            kept.EnergyCost.SetThisTurn(0, reduceOnly: true);
        }

        var toDiscard = hand.Cards.Where(c => !ReferenceEquals(c, kept) && !ReferenceEquals(c, this)).ToList();
        if (toDiscard.Count > 0)
        {
            await CardCmd.Discard(choiceContext, toDiscard);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
