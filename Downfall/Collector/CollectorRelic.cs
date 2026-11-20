using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Collector;

[Pool(typeof(CollectorRelicPool))]
public abstract class CollectorRelic : DownfallRelic
{
    protected override string CharacterKey => TheCollector.CharKey;
}
