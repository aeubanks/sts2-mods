using BaseLib.Utils;
using Downfall.Core;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace Downfall.Awakened;

[Pool(typeof(AwakenedPotionPool))]
public abstract class AwakenedPotion : DownfallPotion
{
    protected override string CharacterKey => TheAwakenedOne.CharKey;

    protected async Task AddRandomPowerToHandAsync()
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        var powers = Owner.Character.CardPool.AllCards
            .Where(c => c.Type == CardType.Power
                && c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();

        if (powers.Count == 0)
            return;

        var chosen = powers[Owner.RunState.Rng.CombatCardGeneration.NextInt(powers.Count)];
        var card = combatState.CreateCard(chosen, Owner);
        if (!card.EnergyCost.CostsX)
        {
            card.SetToFreeThisTurn();
        }
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }
}
