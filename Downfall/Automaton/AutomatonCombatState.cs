using System.Runtime.CompilerServices;
using Downfall.Automaton.Orbs;
using MegaCrit.Sts2.Core.Combat;

namespace Downfall.Automaton;

internal sealed class AutomatonCombatState
{
    private static readonly ConditionalWeakTable<ICombatState, AutomatonCombatState> States = new();

    public List<EncodedCardOrb> EncodedOrbs { get; } = new(AutomatonMechanics.FunctionCapacity);
    public int FunctionsCompiledThisCombat { get; set; }
    public int ExtraFunctionCopiesQueued { get; set; }

    public static AutomatonCombatState Get(ICombatState combatState) =>
        States.GetOrCreateValue(combatState);
}
