using Downfall.Core;

namespace Downfall.Guardian;

public abstract class GuardianPower : DownfallPower
{
    protected override string CharacterKey => TheGuardian.CharKey;
}
