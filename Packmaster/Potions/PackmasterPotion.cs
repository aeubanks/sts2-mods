using BaseLib.Abstracts;
using BaseLib.Utils;
using Packmaster.Character;

namespace Packmaster.Potions;

[Pool(typeof(PackmasterPotionPool))]
public abstract class PackmasterPotion : CustomPotionModel
{
    protected abstract string IconKey { get; }

    public override string CustomPackedImagePath => $"res://Packmaster/images/potions/{IconKey}.png";
    public override string CustomPackedOutlinePath => $"res://Packmaster/images/potions/{IconKey}_outline.png";
}
