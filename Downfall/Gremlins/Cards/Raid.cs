using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Gremlins.Cards;

public sealed class Raid()
    : GremlinCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int drawCount = GremlinMob.GetLivingCount(CombatState, Owner);
        if (drawCount > 0)
        {
            await CardPileCmd.Draw(choiceContext, drawCount, Owner);
        }

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int discardCount = DynamicVars.Cards.IntValue;
        var hand = PileType.Hand.GetPile(Owner).Cards.Where(c => !ReferenceEquals(c, this)).ToList();
        if (hand.Count == 0 || discardCount <= 0)
            return;

        if (hand.Count <= discardCount)
        {
            await CardCmd.Discard(choiceContext, hand);
            return;
        }

        var prefs = new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, discardCount);
        var chosen = (await CardSelectCmd.FromHandForDiscard(choiceContext, Owner, prefs, c => !ReferenceEquals(c, this), this)).ToList();
        if (chosen.Count > 0)
        {
            await CardCmd.Discard(choiceContext, chosen);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(-1m);
    }
}
