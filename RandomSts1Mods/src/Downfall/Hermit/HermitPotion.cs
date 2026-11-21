using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Hermit;

[Pool(typeof(HermitPotionPool))]
public abstract class HermitPotion : DownfallPotion
{
    protected override string CharacterKey => TheHermit.CharKey;
}
