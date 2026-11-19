using BaseLib.Utils;
using Downfall.Core;

namespace Downfall.Gremlins;

[Pool(typeof(GremlinPotionPool))]
public abstract class GremlinPotion : DownfallPotion
{
    protected override string CharacterKey => TheGremlins.CharKey;
}
