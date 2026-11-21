using Downfall.Core;

namespace Downfall.Automaton;

public abstract class BronzePower : DownfallPower
{
    protected override string CharacterKey => TheAutomaton.CharKey;
}
