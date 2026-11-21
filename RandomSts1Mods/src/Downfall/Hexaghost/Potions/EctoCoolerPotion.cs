using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Potions;

/// <summary>
/// Ecto-Cooler (EctoCoolerPotion): Choose 1 of 3 random Ethereal cards and add it
/// (or {Cards} copies) to your Hand. It costs 0 this turn.
/// </summary>
public sealed class EctoCoolerPotion : HexaPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var pool = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                && c.Type is not CardType.Status and not CardType.Curse
                && c.CanonicalKeywords.Contains(CardKeyword.Ethereal))
            .Distinct()
            .ToList();

        if (pool.Count == 0)
        {
            pool = ModelDb.CardPool<HexaCardPool>().AllCards
                .Where(c => c.CanBeGeneratedInCombat
                    && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                    && c.CanonicalKeywords.Contains(CardKeyword.Ethereal))
                .Distinct()
                .ToList();
        }

        if (pool.Count == 0)
            return;

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        int pickCount = Math.Min(3, pool.Count);
        var choices = new List<CardModel>(pickCount);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = rng.NextInt(pool.Count);
            var canonical = pool[idx];
            pool.RemoveAt(idx);

            var preview = combatState.CreateCard(canonical, Owner);
            if (!preview.EnergyCost.CostsX)
                preview.SetToFreeThisTurn();
            choices.Add(preview);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: true);
        if (chosen is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int copies = DynamicVars.Cards.IntValue;
        for (int i = 0; i < copies; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var copy = i == 0 ? chosen : combatState.CreateCard(chosen.CanonicalInstance, Owner);
            if (!copy.EnergyCost.CostsX)
                copy.SetToFreeThisTurn();
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);
        }
    }
}
