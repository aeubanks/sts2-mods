using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class Gather()
    : AwakenedCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override string PortraitKey => "gather";

    public override bool UsesChant => true;
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        int chantTriggers = TryTriggerChantCount(cardPlay);
        if (chantTriggers <= 0)
            return;

        var hand = PileType.Hand.GetPile(Owner);
        var discard = PileType.Discard.GetPile(Owner).Cards.ToList();
        int count = Math.Min(chantTriggers, Math.Min(discard.Count, CardPile.MaxCardsInHand - hand.Cards.Count));
        if (count <= 0)
            return;

        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, count);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, discard, Owner, prefs)).ToList();
        foreach (var card in chosen)
        {
            if (hand.Cards.Count >= CardPile.MaxCardsInHand)
                break;

            await CardPileCmd.Add(card, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
