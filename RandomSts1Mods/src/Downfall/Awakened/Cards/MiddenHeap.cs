using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Cards;

public sealed class MiddenHeap()
    : AwakenedCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Move),
        new CardsVar(1)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (Owner.PlayerCombatState is not { } pcs)
            return;

        var candidates = pcs.DrawPile.Cards
            .Concat(pcs.DiscardPile.Cards)
            .Where(c => c.Type is CardType.Status or CardType.Curse)
            .ToList();

        if (candidates.Count == 0)
            return;

        int selectCount = Math.Min(DynamicVars.Cards.IntValue, candidates.Count);
        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, selectCount);
        var selected = (await CardSelectCmd.FromSimpleGrid(choiceContext, candidates, Owner, prefs)).ToList();

        foreach (var card in selected)
        {
            if (pcs.Hand.Cards.Count < CardPile.MaxCardsInHand)
            {
                await CardPileCmd.Add(card, PileType.Hand);
            }
            else if (card.Pile?.Type == PileType.Draw)
            {
                await CardPileCmd.Add(card, PileType.Discard);
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
