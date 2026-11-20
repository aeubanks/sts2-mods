using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Guardian;

[Pool(typeof(GuardianRelicPool))]
public abstract class GuardianRelic : DownfallRelic
{
    protected override string CharacterKey => TheGuardian.CharKey;
}
