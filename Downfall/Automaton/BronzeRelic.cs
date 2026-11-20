using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Automaton;

[Pool(typeof(AutomatonRelicPool))]
public abstract class BronzeRelic : DownfallRelic
{
    protected override string CharacterKey => TheAutomaton.CharKey;
}
