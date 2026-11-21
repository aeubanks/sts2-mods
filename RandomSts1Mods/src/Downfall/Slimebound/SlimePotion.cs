using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Slimebound;

[Pool(typeof(SlimeboundPotionPool))]
public abstract class SlimePotion : DownfallPotion
{
    protected override string CharacterKey => TheSlimeBoss.CharKey;
}
