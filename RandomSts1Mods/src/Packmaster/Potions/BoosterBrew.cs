using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Packmaster.Character;
using Packmaster.Packs;

namespace Packmaster.Potions;

public sealed class BoosterBrew : PackmasterPotion
{
    private const int PackChoiceCount = 3;

    protected override string IconKey => "booster_brew";

    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(5)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);
        var player = target!.Player!;
        var combatState = player.Creature.CombatState;
        if (combatState is null)
            return;

        var rng = player.RunState.Rng.CombatCardGeneration;
        var cardsByPack = ModelDb.CardPool<PackmasterCardPool>().AllCards
            .Where(c => c.CanBeGeneratedInCombat)
            .Select(c => (Card: c, Pack: PackRegistry.GetForCard(c)))
            .Where(x => x.Pack is not null)
            .GroupBy(x => x.Pack!)
            .Select(g => g.Select(x => x.Card).ToList())
            .ToList();

        if (cardsByPack.Count == 0)
            return;

        var offered = new List<(CardModel Preview, List<CardModel> Cards)>();
        int choicesToOffer = Math.Min(PackChoiceCount, cardsByPack.Count);
        for (int i = 0; i < choicesToOffer; i++)
        {
            int idx = rng.NextInt(cardsByPack.Count);
            var cards = cardsByPack[idx];
            cardsByPack.RemoveAt(idx);
            var preview = combatState.CreateCard(cards[rng.NextInt(cards.Count)], player);
            offered.Add((preview, cards));
        }

        var picked = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            offered.Select(o => o.Preview).ToList(),
            player,
            canSkip: false);

        var chosenCards = offered.FirstOrDefault(o => ReferenceEquals(o.Preview, picked)).Cards;
        if (chosenCards is null)
            return;

        int count = DynamicVars.Cards.IntValue;
        var generated = new List<CardModel>(count);
        for (int i = 0; i < count; i++)
        {
            var canonical = chosenCards[rng.NextInt(chosenCards.Count)];
            var card = combatState.CreateCard(canonical, player);
            if (!card.EnergyCost.CostsX && card.EnergyCost.Canonical > 0)
            {
                card.EnergyCost.SetThisCombat(0);
            }
            generated.Add(card);
        }

        await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Draw, player, CardPilePosition.Random);
    }
}
