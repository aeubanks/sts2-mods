using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Hexaghost;

[Pool(typeof(HexaRelicPool))]
public abstract class HexaRelic : DownfallRelic
{
    protected override string CharacterKey => TheHexaghost.CharKey;
}
