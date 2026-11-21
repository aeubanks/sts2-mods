using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Collector;

[Pool(typeof(CollectorPotionPool))]
public abstract class CollectorPotion : DownfallPotion
{
    protected override string CharacterKey => TheCollector.CharKey;
}
