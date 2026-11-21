using Downfall.Core;

namespace Downfall.Champ;

public abstract class ChampPower : DownfallPower
{
    protected override string CharacterKey => TheChamp.CharKey;
}
