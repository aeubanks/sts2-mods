using BaseLib.Abstracts;

namespace Downfall.Core;

// Shared potion base across all Downfall characters — intentionally carries NO [Pool(...)] attribute.
public abstract class DownfallPotion : CustomPotionModel
{
    protected abstract string CharacterKey { get; }
    protected abstract string IconKey { get; }

    public override string CustomPackedImagePath => $"res://Downfall/images/{CharacterKey}/potions/{IconKey}.png";
    public override string CustomPackedOutlinePath => $"res://Downfall/images/{CharacterKey}/potions/{IconKey}_outline.png";
}
