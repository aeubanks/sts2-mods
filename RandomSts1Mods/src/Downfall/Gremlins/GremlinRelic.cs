using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Gremlins;

[Pool(typeof(GremlinRelicPool))]
public abstract class GremlinRelic : DownfallRelic
{
    protected override string CharacterKey => TheGremlins.CharKey;
}
