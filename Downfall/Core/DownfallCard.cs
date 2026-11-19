using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;

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
    protected abstract string PortraitKey { get; }

    public override string PortraitPath => $"res://Downfall/images/{CharacterKey}/cards/{PortraitKey}.png";
    public override string CustomPortraitPath => $"res://Downfall/images/{CharacterKey}/cards/big/{PortraitKey}.png";
}
