using Downfall.Core;

namespace Downfall.Slimebound;

public abstract class SlimePower : DownfallPower
{
    protected override string CharacterKey => TheSlimeBoss.CharKey;
}
