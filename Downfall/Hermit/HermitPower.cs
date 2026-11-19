using Downfall.Core;

namespace Downfall.Hermit;

public abstract class HermitPower : DownfallPower
{
    protected override string CharacterKey => TheHermit.CharKey;
}
