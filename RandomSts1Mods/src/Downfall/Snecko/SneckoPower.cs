using Downfall.Core;

namespace Downfall.Snecko;

public abstract class SneckoPower : DownfallPower
{
    protected override string CharacterKey => TheSnecko.CharKey;
}
