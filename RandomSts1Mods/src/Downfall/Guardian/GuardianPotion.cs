using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Guardian;

[Pool(typeof(GuardianPotionPool))]
public abstract class GuardianPotion : DownfallPotion
{
    protected override string CharacterKey => TheGuardian.CharKey;
}
