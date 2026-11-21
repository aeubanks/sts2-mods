using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;

namespace Downfall.Core;

// Shared card base across all Downfall characters — intentionally carries NO [Pool(...)] attribute.
public abstract class DownfallCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : CustomCardModel(cost, type, rarity, target, showInCardLibrary)
{
    protected abstract string CharacterKey { get; }
    protected virtual string PortraitKey => StringHelper.Slugify(GetType().Name).ToLowerInvariant();

    public override string PortraitPath => $"res://Downfall/images/{CharacterKey}/cards/{PortraitKey}.png";
    public override string CustomPortraitPath => $"res://Downfall/images/{CharacterKey}/cards/big/{PortraitKey}.png";
}
