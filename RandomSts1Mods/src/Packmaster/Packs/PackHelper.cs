using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Packs;

public static class PackHelper
{
    public static bool HasSynergy(CardModel card)
    {
        var hand = card.Owner?.PlayerCombatState?.Hand.Cards;
        if (hand is null)
            return false;

        var myPack = PackRegistry.GetForCard(card);
        PackDefinition? firstOtherPack = null;

        foreach (var other in hand)
        {
            if (ReferenceEquals(other, card))
                continue;

            var otherPack = PackRegistry.GetForCard(other);
            if (otherPack is null || ReferenceEquals(otherPack, myPack))
                continue;

            if (firstOtherPack is null)
                firstOtherPack = otherPack;
            else if (!ReferenceEquals(firstOtherPack, otherPack))
                return true;
        }

        return false;
    }

    public static async Task FetchDistinctFromDrawAsync<TKey>(
        Player owner,
        int count,
        Func<CardModel, TKey?> keySelector)
    {
        var drawPile = PileType.Draw.GetPile(owner);
        var candidates = drawPile.Cards
            .Where(c => keySelector(c) is not null)
            .ToList();

        var rng = owner.RunState.Rng.CombatCardSelection;
        var comparer = EqualityComparer<TKey?>.Default;

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            candidates.RemoveAll(c => c.Pile != drawPile);
            if (candidates.Count == 0)
                break;

            var chosen = candidates[rng.NextInt(candidates.Count)];
            var chosenKey = keySelector(chosen);
            candidates.RemoveAll(c =>
            {
                var key = keySelector(c);
                return key is null || comparer.Equals(key, chosenKey);
            });

            await CardPileCmd.Add(chosen, PileType.Hand);
        }
    }
}
