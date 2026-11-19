using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using Packmaster.Character;

namespace Packmaster.Cards;

[Pool(typeof(PackmasterCardPool))]
public abstract class PackmasterCard(int cost, CardType type, CardRarity rarity, TargetType target)
    : CustomCardModel(cost, type, rarity, target)
{
    protected abstract string PortraitKey { get; }

    public override string PortraitPath => $"res://Packmaster/images/card_portraits/{PortraitKey}.png";
    public override string CustomPortraitPath => $"res://Packmaster/images/card_portraits/big/{PortraitKey}.png";
}
