using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Core;

/// <summary>
/// Shared Downfall Echo helper (Ethereal + Exhaust non-Power clone, non-recursive).
/// Used by Gremlins, Champ (Specter's Hand, Triple Strike), and future Downfall characters.
/// </summary>
internal static class DownfallEcho
{
    private static readonly ConditionalWeakTable<CardModel, object> EchoCards = new();
    private static readonly object EchoMarker = new();

    public static bool IsEcho(CardModel card) => EchoCards.TryGetValue(card, out _);

    public static CardModel? MakeEcho(CardModel source)
    {
        if (!source.IsMutable || IsEcho(source))
            return null;

        var clone = source.CreateClone();
        clone.AddKeyword(CardKeyword.Ethereal);
        if (clone.Type != CardType.Power)
        {
            clone.AddKeyword(CardKeyword.Exhaust);
        }
        EchoCards.AddOrUpdate(clone, EchoMarker);
        return clone;
    }
}
