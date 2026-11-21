using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Guardian.Cards;

public sealed class Preprogram() : GuardianCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override int MaxSockets => 1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(5)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        StasisHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawPile = PileType.Draw.GetPile(Owner);
        var discardPile = PileType.Discard.GetPile(Owner);
        if (drawPile.Cards.Count == 0 && discardPile.Cards.Count > 0)
        {
            await CardPileCmd.ShuffleIfNecessary(choiceContext, Owner);
        }

        if (drawPile.Cards.Count > 0 && GuardianMechanics.CanPlaceInStasis(Owner))
        {
            var topCards = drawPile.Cards.Take(DynamicVars.Cards.IntValue).ToList();
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 0, 1);
            var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, topCards, Owner, prefs)).FirstOrDefault();
            if (chosen is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await GuardianMechanics.PlaceInStasisAsync(choiceContext, Owner, chosen);
            }
        }

        await TriggerSocketedGemsAsync(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(3m);
    }
}
