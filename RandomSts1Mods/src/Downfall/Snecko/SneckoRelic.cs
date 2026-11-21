using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Snecko;

[Pool(typeof(SneckoRelicPool))]
public abstract class SneckoRelic : DownfallRelic
{
    protected override string CharacterKey => TheSnecko.CharKey;
}
