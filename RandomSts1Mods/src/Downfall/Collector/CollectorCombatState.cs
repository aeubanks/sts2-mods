using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector;

/// <summary>
/// Per-combat Collector state scoped cleanly to ICombatState via ConditionalWeakTable.
/// Holds each player's CombatCollection pile, CollectedCardsPlayedThisCombat, and LastTurnUnusedBlock.
/// </summary>
public sealed class CollectorCombatState
{
    private static readonly ConditionalWeakTable<ICombatState, CollectorCombatState> States = new();

    private readonly Dictionary<Player, PlayerData> _players = new(ReferenceEqualityComparer.Instance);

    public sealed class PlayerData
    {
        /// <summary>
        /// Remaining cards in the player's Combat Collection pile (index 0 = top of pile).
        /// </summary>
        public List<CardModel> CombatCollection { get; } = [];

        public int CollectedCardsPlayedThisCombat { get; set; }

        public int BlockBeforeTurnStartClear { get; set; }

        public int LastTurnUnusedBlock { get; set; }
    }

    public static CollectorCombatState Get(ICombatState combatState) =>
        States.GetOrCreateValue(combatState);

    public PlayerData GetPlayer(Player player)
    {
        if (!_players.TryGetValue(player, out var data))
        {
            data = new PlayerData();
            _players[player] = data;
        }
        return data;
    }
}
