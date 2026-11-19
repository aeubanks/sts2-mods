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
}
