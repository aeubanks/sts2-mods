using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Snecko;

[Pool(typeof(SneckoPotionPool))]
public abstract class SneckoPotion : DownfallPotion
{
    protected override string CharacterKey => TheSnecko.CharKey;
}
