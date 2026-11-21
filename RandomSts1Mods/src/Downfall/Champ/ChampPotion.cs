using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Champ;

[Pool(typeof(ChampPotionPool))]
public abstract class ChampPotion : DownfallPotion
{
    protected override string CharacterKey => TheChamp.CharKey;
}
