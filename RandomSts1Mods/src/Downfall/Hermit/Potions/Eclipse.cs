using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hermit.Potions;

public sealed class Eclipse : HermitPotion
{
    protected override string IconKey => "eclipse";

    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var hand = PileType.Hand.GetPile(Owner);
        var exhaustCards = PileType.Exhaust.GetPile(Owner).Cards.ToList();
        if (exhaustCards.Count == 0 || hand.Cards.Count >= CardPile.MaxCardsInHand)
            return;

        int maxPick = Math.Min(DynamicVars.Cards.IntValue, exhaustCards.Count);
        var prompt = new LocString("potions", "DOWNFALL-ECLIPSE.selectionScreenPrompt");
        var prefs = new CardSelectorPrefs(prompt, maxPick);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, exhaustCards, Owner, prefs)).ToList();

        foreach (var card in chosen)
        {
            if (hand.Cards.Count >= CardPile.MaxCardsInHand)
                break;

            await CardPileCmd.Add(card, PileType.Hand);
            if (!card.EnergyCost.CostsX && card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            {
                card.EnergyCost.SetThisTurn(0, reduceOnly: true);
            }
        }
    }
}
