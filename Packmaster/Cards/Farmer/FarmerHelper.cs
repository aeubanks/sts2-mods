using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Packmaster.Cards.Farmer;

internal static class FarmerHelper
{
    public static int GetDistinctCardTypesPlayedThisTurn(
        Player? owner,
        ICombatState? combatState,
        CardPlay? currentPlay = null)
    {
        if (owner is null || combatState is null)
            return 0;

        var types = new HashSet<CardType>();
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.CardPlay.Card.Owner != owner)
                continue;

            if (currentPlay is not null && ReferenceEquals(entry.CardPlay, currentPlay))
                break;

            if (entry.HappenedThisTurn(combatState))
            {
                types.Add(entry.CardPlay.Card.Type);
            }
        }
        return types.Count;
    }

    public static CardType? GetPreviousCardTypePlayedThisCombat(Player? owner, CardPlay? currentPlay = null)
    {
        if (owner is null)
            return null;

        CardType? previous = null;
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.CardPlay.Card.Owner != owner)
                continue;

            if (currentPlay is not null && ReferenceEquals(entry.CardPlay, currentPlay))
                return previous;

            previous = entry.CardPlay.Card.Type;
        }
        return previous;
    }
}
