using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Hermit;

[Pool(typeof(HermitRelicPool))]
public abstract class HermitRelic : DownfallRelic
{
    protected override string CharacterKey => TheHermit.CharKey;
}
