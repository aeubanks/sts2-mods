using Downfall.Core;

namespace Downfall.Awakened;

public abstract class AwakenedPower : DownfallPower
{
    protected override string CharacterKey => TheAwakenedOne.CharKey;
}
