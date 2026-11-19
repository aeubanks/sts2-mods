using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Packmaster.Cards.EvenOdd;

internal static class EvenOddHelper
{
    public static int CountPlayedThisTurn(Player? owner, ICombatState? combatState)
    {
        if (owner is null || combatState is null)
            return 0;

        int count = 0;
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.HappenedThisTurn(combatState) && entry.CardPlay.Card.Owner == owner)
            {
                count++;
            }
        }
        return count;
    }

    // For hand/glow/targeting checks BEFORE this card starts playing (1-based next play index = count + 1).
    public static bool IsNextPlayFirst(Player? owner, ICombatState? combatState) =>
        owner is not null && combatState is not null && CountPlayedThisTurn(owner, combatState) == 0;

    public static bool IsNextPlayOdd(Player? owner, ICombatState? combatState) =>
        owner is not null && combatState is not null && CountPlayedThisTurn(owner, combatState) % 2 == 0;

    public static bool IsNextPlayEven(Player? owner, ICombatState? combatState) =>
        owner is not null && combatState is not null && CountPlayedThisTurn(owner, combatState) % 2 == 1;

    // For OnPlay / AfterCardPlayed checks AFTER CardPlayStarted has already been logged (1-based current play index = count).
    public static bool IsCurrentPlayFirst(Player? owner, ICombatState? combatState) =>
        owner is not null && combatState is not null && CountPlayedThisTurn(owner, combatState) == 1;

    public static bool IsCurrentPlayOdd(Player? owner, ICombatState? combatState) =>
        owner is not null && combatState is not null && CountPlayedThisTurn(owner, combatState) % 2 == 1;

    public static bool IsCurrentPlayEven(Player? owner, ICombatState? combatState)
    {
        if (owner is null || combatState is null)
            return false;

        int count = CountPlayedThisTurn(owner, combatState);
        return count > 0 && count % 2 == 0;
    }
}
