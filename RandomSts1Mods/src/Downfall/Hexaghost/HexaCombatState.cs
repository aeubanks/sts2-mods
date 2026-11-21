using System.Runtime.CompilerServices;
using Downfall.Hexaghost.Orbs;
using MegaCrit.Sts2.Core.Combat;

namespace Downfall.Hexaghost;

/// <summary>
/// Per-combat Hexaghost wheel and turn/combat tracking, scoped via ConditionalWeakTable
/// so state never leaks across combats or runs.
/// </summary>
public sealed class HexaCombatState
{
    private static readonly ConditionalWeakTable<ICombatState, HexaCombatState> States = new();

    public static HexaCombatState Get(ICombatState combatState) => States.GetOrCreateValue(combatState);

    public List<GhostflameOrb> Ghostflames { get; } = new(HexaMechanics.WheelSlotCount);
    public bool WheelInitialized { get; set; }
    public int ActiveSlotIndex { get; set; }
    public bool WasActiveIgnitedBeforeCardPlay { get; set; }

    public int IgnitionsThisTurn { get; set; }
    public int IgnitionsThisCombat { get; set; }
    public int AdvancesThisTurn { get; set; }
    public int AdvancesThisCombat { get; set; }
    public int CardsExhaustedThisTurn { get; set; }
    public int CardsExhaustedLastTurn { get; set; }
    public int EtherealExhaustsThisTurn { get; set; }
    public int SoulburnAppliedThisCombat { get; set; }

    public void OnPlayerTurnStarted()
    {
        CardsExhaustedLastTurn = CardsExhaustedThisTurn;
        CardsExhaustedThisTurn = 0;
        EtherealExhaustsThisTurn = 0;
        IgnitionsThisTurn = 0;
        AdvancesThisTurn = 0;
    }
}
