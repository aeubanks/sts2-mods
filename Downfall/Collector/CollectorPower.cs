using Downfall.Core;

namespace Downfall.Collector;

public abstract class CollectorPower : DownfallPower
{
    protected override string CharacterKey => TheCollector.CharKey;
}
