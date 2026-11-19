using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Cards.EnergyAndEcho;

internal static class EchoHelper
{
    public static IEnumerable<IHoverTip> EchoHoverTips =>
    [
        new HoverTip(
            new LocString("gameplay_ui", "PACKMASTER-ECHO_KEYWORD.title"),
            new LocString("gameplay_ui", "PACKMASTER-ECHO_KEYWORD.description")),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    public static int GetEnergySpentThisTurn(Player? owner, ICombatState? combatState)
    {
        if (owner is null || combatState is null)
            return 0;

        int total = 0;
        foreach (var entry in CombatManager.Instance.History.Entries.OfType<EnergySpentEntry>())
        {
            if (entry.HappenedThisTurn(combatState) && entry.Actor == owner.Creature)
            {
                total += entry.Amount;
            }
        }
        return total;
    }

    public static async Task AddEchoCopyAsync<T>(T source) where T : CardModel
    {
        if (source.CombatState is null
            || CombatManager.Instance.IsOverOrEnding
            || !source.Owner.Creature.IsAlive)
            return;

        var copy = source.CombatState.CreateCard<T>(source.Owner);
        if (source.IsUpgraded)
        {
            CardCmd.Upgrade(copy);
        }
        CardCmd.ApplyKeyword(copy, CardKeyword.Exhaust, CardKeyword.Ethereal);
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, source.Owner);
    }
}
