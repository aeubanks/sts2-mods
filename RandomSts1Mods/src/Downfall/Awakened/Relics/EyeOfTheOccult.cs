using Downfall.Awakened.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Awakened.Relics;

public sealed class EyeOfTheOccult : AwakenedRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Thunderbolt>(),
        HoverTipFactory.FromCard<Darkleech>()
    ];

    internal void TriggerFlash() => Flash();
}
