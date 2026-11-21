using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Awakened.Potions;

public sealed class SneckoPowersPotion : AwakenedPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        var pool = ModelDb.AllCharacters
            .Where(c => c.CardPool != Owner.Character.CardPool)
            .SelectMany(c => c.CardPool.AllCards)
            .Where(c => c.Type == CardType.Power
                && c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .Distinct()
            .ToList();

        if (pool.Count == 0)
            return;

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        var choices = new List<CardModel>(3);
        int pickCount = Math.Min(3, pool.Count);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = rng.NextInt(pool.Count);
            var canonical = pool[idx];
            pool.RemoveAt(idx);
            var preview = combatState.CreateCard(canonical, Owner);
            if (!preview.EnergyCost.CostsX)
            {
                preview.SetToFreeThisTurn();
            }
            choices.Add(preview);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: true);
        if (chosen is null)
            return;

        int copies = DynamicVars.Cards.IntValue;
        for (int i = 0; i < copies; i++)
        {
            var copy = i == 0 ? chosen : combatState.CreateCard(chosen.CanonicalInstance, Owner);
            if (!copy.EnergyCost.CostsX)
            {
                copy.SetToFreeThisTurn();
            }
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);
        }
    }
}
