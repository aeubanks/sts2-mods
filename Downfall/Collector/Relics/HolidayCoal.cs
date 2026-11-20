using Downfall.Collector.Cards.Collectibles;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Relics;

/// <summary>
/// Krampian Coal (HolidayCoal, Shop):
/// When you would draw from an empty Collected pile, add a Lucky Wick into your hand instead.
/// </summary>
public sealed class HolidayCoal : CollectorRelic, IEmptyCollectionDrawProvider
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.CollectedHoverTip,
        HoverTipFactory.FromCard<LuckyWick>()
    ];

    public CardModel? TryCreateFallbackCollectible(ICombatState combatState, Player player)
    {
        Flash();
        return combatState.CreateCard<LuckyWick>(player);
    }
}
