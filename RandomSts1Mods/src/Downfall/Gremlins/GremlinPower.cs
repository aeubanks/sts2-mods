using Downfall.Core;

namespace Downfall.Gremlins;

public abstract class GremlinPower : DownfallPower
{
    protected override string CharacterKey => TheGremlins.CharKey;
}
