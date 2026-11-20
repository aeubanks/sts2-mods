using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace Downfall.Snecko;

internal static class SneckoOffclass
{
    internal static bool IsOffclass(CardModel card, Player owner) =>
        card.Pool != owner.Character.CardPool
        && card.Type is CardType.Attack or CardType.Skill or CardType.Power;

    internal static List<CardModel> GetOffclassPool(
        Player owner,
        bool inCombat,
        Func<CardModel, bool>? predicate = null)
    {
        var list = ModelDb.AllCharacters
            .Where(c => c.CardPool != owner.Character.CardPool)
            .SelectMany(c => c.CardPool.GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint))
            .Where(c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                && c.Type is CardType.Attack or CardType.Skill or CardType.Power
                && (!inCombat || c.CanBeGeneratedInCombat)
                && (inCombat || c is not SneckoCard { IsGift: true })
                && (predicate is null || predicate(c)))
            .Distinct()
            .ToList();

        return list;
    }

    internal static bool IsDebuffCard(CardModel card)
    {
        if (card.TargetType is not (TargetType.AnyEnemy or TargetType.AllEnemies or TargetType.RandomEnemy))
            return false;

        foreach (var tip in card.HoverTips)
        {
            if (tip is HoverTip ht
                && (ht.IsDebuff || (ht.CanonicalModel is PowerModel pm && pm.Type == PowerType.Debuff)))
            {
                return true;
            }
        }

        return false;
    }

    internal static CardModel? GenerateRandomOffclassCombatCard(
        ICombatState combatState,
        Player owner,
        Func<CardModel, bool>? predicate = null)
    {
        var pool = GetOffclassPool(owner, inCombat: true, predicate);
        if (pool.Count == 0)
            return null;

        var canonical = pool[owner.RunState.Rng.CombatCardGeneration.NextInt(pool.Count)];
        return combatState.CreateCard(canonical, owner);
    }

    internal static List<CardModel> GenerateDistinctOffclassDeckCards(
        Player owner,
        int count,
        Func<CardModel, bool>? predicate = null,
        bool upgradeAll = false,
        Rng? rng = null)
    {
        var pool = GetOffclassPool(owner, inCombat: false, predicate);
        if (pool.Count == 0)
            return [];

        rng ??= owner.RunState.Rng.Niche;
        int pickCount = Math.Min(count, pool.Count);
        var results = new List<CardCreationResult>(pickCount);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = rng.NextInt(pool.Count);
            var canonical = pool[idx];
            pool.RemoveAt(idx);

            var card = owner.RunState.CreateCard(canonical, owner);
            if (upgradeAll && card.IsUpgradable)
            {
                CardCmd.Upgrade(card);
            }
            results.Add(new CardCreationResult(card));
        }

        if (!upgradeAll)
        {
            var options = new CardCreationOptions(Array.Empty<CardPoolModel>(), CardCreationSource.Other, CardRarityOddsType.Uniform)
                .WithFlags(CardCreationFlags.NoCardPoolModifications);
            Hook.TryModifyCardRewardOptions(owner.RunState, owner, results, options, out _);
        }

        return results.Select(r => r.Card).ToList();
    }

    internal static async Task<CardModel?> OfferOffclassDeckChoiceAsync(
        Player owner,
        int choiceCount,
        Func<CardModel, bool>? predicate = null,
        bool upgradeAll = false,
        Rng? rng = null)
    {
        var choices = GenerateDistinctOffclassDeckCards(owner, choiceCount, predicate, upgradeAll, rng);
        if (choices.Count == 0)
            return null;

        var chosen = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            choices,
            owner,
            canSkip: true);

        if (chosen is null)
            return null;

        await CardPileCmd.Add(chosen, PileType.Deck);
        return chosen;
    }

    internal static int CountUniqueSuitsInHand(Player? owner, CardModel? includeCard = null)
    {
        if (owner?.PlayerCombatState is null)
            return 0;

        var pools = new HashSet<CardPoolModel>();
        foreach (var card in PileType.Hand.GetPile(owner).Cards)
        {
            if (card.Rarity == CardRarity.Token || card.Type is not (CardType.Attack or CardType.Skill or CardType.Power))
                continue;
            pools.Add(card.Pool);
        }
        if (includeCard is not null
            && includeCard.Rarity != CardRarity.Token
            && includeCard.Type is CardType.Attack or CardType.Skill or CardType.Power)
        {
            pools.Add(includeCard.Pool);
        }
        return pools.Count;
    }

    internal static int CountUniqueSuitsPlayedThisTurn(Player? owner)
    {
        if (owner?.Creature.CombatState is not { } combatState)
            return 0;

        var pools = new HashSet<CardPoolModel>();
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (!entry.HappenedThisTurn(combatState))
                continue;
            if (entry.CardPlay.Card.Owner != owner)
                continue;
            if (entry.CardPlay.Card.Rarity == CardRarity.Token
                || entry.CardPlay.Card.Type is not (CardType.Attack or CardType.Skill or CardType.Power))
                continue;
            pools.Add(entry.CardPlay.Card.Pool);
        }
        return pools.Count;
    }
}
