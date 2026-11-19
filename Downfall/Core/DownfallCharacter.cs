using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using Godot;

namespace Downfall.Core;

public abstract class DownfallCharacter : PlaceholderCharacterModel
{
    public abstract string CharacterKey { get; }

    public override Control CustomIcon
    {
        get
        {
            var icon = NodeFactory<Control>.CreateFromResource(CustomIconTexturePath);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }

    public string SpriteTexturePath => $"res://Downfall/images/{CharacterKey}/char/{CharacterKey}.png";

    public override string CustomVisualPath => $"res://Downfall/scenes/{CharacterKey}/{CharacterKey}_visuals.tscn";
    public override string CustomRestSiteAnimPath => $"res://Downfall/scenes/{CharacterKey}/{CharacterKey}_rest_site.tscn";
    public override string CustomMerchantAnimPath => $"res://Downfall/scenes/{CharacterKey}/{CharacterKey}_merchant.tscn";
    public override string CustomIconTexturePath => $"res://Downfall/images/{CharacterKey}/charui/character_icon.png";
    public override string CustomIconOutlineTexturePath => $"res://Downfall/images/{CharacterKey}/charui/character_icon_outline.png";
    public override string CustomCharacterSelectIconPath => $"res://Downfall/images/{CharacterKey}/charui/char_select.png";
    public override string CustomCharacterSelectLockedIconPath => $"res://Downfall/images/{CharacterKey}/charui/char_select_locked.png";
    public override string CustomCharacterSelectBg => $"res://Downfall/scenes/{CharacterKey}/char_select_bg.tscn";
    public override string CustomMapMarkerPath => $"res://Downfall/images/{CharacterKey}/charui/map_marker.png";
}
