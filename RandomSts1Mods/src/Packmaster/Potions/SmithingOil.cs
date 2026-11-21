using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Potions;

public sealed class SmithingOil : PackmasterPotion
{
    protected override string IconKey => "smithing_oil";

    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);
        var player = target!.Player!;

        var hand = PileType.Hand.GetPile(player);
        int eligibleCount = hand.Cards.Count(CanUpgradeCombatOrDeck);
        if (eligibleCount == 0)
            return;

        int selectCount = Math.Min(DynamicVars.Cards.IntValue, eligibleCount);
        if (selectCount <= 0)
            return;

        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, selectCount);
        var selected = (await CardSelectCmd.FromHand(choiceContext, player, prefs, CanUpgradeCombatOrDeck, this)).ToList();

        foreach (var card in selected)
        {
            if (card.IsUpgradable)
            {
                CardCmd.Upgrade(card);
            }
            if (card.DeckVersion is { IsUpgradable: true } deckCard)
            {
                CardCmd.Upgrade(deckCard);
            }
        }
    }

    private static bool CanUpgradeCombatOrDeck(CardModel card) =>
        card.IsUpgradable || (card.DeckVersion?.IsUpgradable ?? false);
}
