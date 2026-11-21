using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Hexaghost;

[Pool(typeof(HexaPotionPool))]
public abstract class HexaPotion : DownfallPotion
{
    protected override string CharacterKey => TheHexaghost.CharKey;
}
