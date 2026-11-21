using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Automaton;

[Pool(typeof(AutomatonPotionPool))]
public abstract class BronzePotion : DownfallPotion
{
    protected override string CharacterKey => TheAutomaton.CharKey;
}
