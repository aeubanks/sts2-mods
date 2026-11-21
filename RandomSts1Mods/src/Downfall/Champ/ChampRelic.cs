using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Champ;

[Pool(typeof(ChampRelicPool))]
public abstract class ChampRelic : DownfallRelic
{
    protected override string CharacterKey => TheChamp.CharKey;
}
