using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Slimebound;

[Pool(typeof(SlimeboundRelicPool))]
public abstract class SlimeRelic : DownfallRelic
{
    protected override string CharacterKey => TheSlimeBoss.CharKey;
}
