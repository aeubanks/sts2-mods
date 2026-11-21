using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using Packmaster.Cards;
using Packmaster.Relics;

namespace Packmaster.Character;

public class ThePackmaster : PlaceholderCharacterModel
{
    public const string CharacterId = "ThePackmaster";

    public static readonly Color CharacterColor = new(0.76f, 0.65f, 0.52f);

    public override Color NameColor => CharacterColor;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 75;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<PackmasterStrike>(),
        ModelDb.Card<PackmasterStrike>(),
        ModelDb.Card<PackmasterStrike>(),
        ModelDb.Card<PackmasterStrike>(),
        ModelDb.Card<PackmasterDefend>(),
        ModelDb.Card<PackmasterDefend>(),
        ModelDb.Card<PackmasterDefend>(),
        ModelDb.Card<PackmasterDefend>(),
        ModelDb.Card<Rummage>(),
        ModelDb.Card<Cardistry>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<HandyHaversack>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<PackmasterCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<PackmasterRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<PackmasterPotionPool>();

    public override Control CustomIcon
    {
        get
        {
            var icon = NodeFactory<Control>.CreateFromResource(CustomIconTexturePath);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }

    public override string CustomVisualPath => "res://Packmaster/scenes/packmaster_visuals.tscn";
    public override string CustomRestSiteAnimPath => "res://Packmaster/scenes/packmaster_rest_site.tscn";
    public override string CustomMerchantAnimPath => "res://Packmaster/scenes/packmaster_merchant.tscn";
    public override string CustomIconTexturePath => "res://Packmaster/images/charui/character_icon_packmaster.png";
    public override string CustomIconOutlineTexturePath => "res://Packmaster/images/charui/character_icon_packmaster_outline.png";
    public override string CustomCharacterSelectIconPath => "res://Packmaster/images/charui/char_select_packmaster.png";
    public override string CustomCharacterSelectLockedIconPath => "res://Packmaster/images/charui/char_select_packmaster_locked.png";
    public override string CustomCharacterSelectBg => "res://Packmaster/scenes/char_select_bg_packmaster.tscn";
    public override string CustomMapMarkerPath => "res://Packmaster/images/charui/map_marker_packmaster.png";
}
