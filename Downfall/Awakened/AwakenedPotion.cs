using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Awakened;

[Pool(typeof(AwakenedPotionPool))]
public abstract class AwakenedPotion : DownfallPotion
{
    protected override string CharacterKey => TheAwakenedOne.CharKey;
}
