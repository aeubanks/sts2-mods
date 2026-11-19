using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using Packmaster.Character;
using Packmaster.Packs;

namespace Packmaster.Cards;

[Pool(typeof(PackmasterCardPool))]
public abstract class PackmasterCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : CustomCardModel(cost, type, rarity, target, showInCardLibrary), ICustomTypeTextCard
{
    protected abstract string PortraitKey { get; }

    public override string PortraitPath => $"res://Packmaster/images/card_portraits/{PortraitKey}.png";
    public override string CustomPortraitPath => $"res://Packmaster/images/card_portraits/big/{PortraitKey}.png";

    protected bool HasSynergy => PackHelper.HasSynergy(this);

    public IEnumerable<LocString> GetTypeModifiers()
    {
        var pack = PackRegistry.GetForCard(this);
        if (pack is null)
            return [];

        var packTitle = pack.Title;
        var packName = packTitle.Exists() ? packTitle.GetFormattedText() : pack.Id;
        var plaque = new LocString("gameplay_ui", "PACKMASTER-PACK_CARD_TYPE");
        if (!plaque.Exists())
            return [];

        plaque.Add("Pack", packName);
        return [plaque];
    }
}
