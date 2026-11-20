using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Snecko.Relics;

public sealed class RareBoosterPack : SneckoRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.OffclassHoverTip
    ];

    public override async Task AfterObtained()
    {
        Flash();
        await SneckoOffclass.OfferOffclassDeckChoiceAsync(
            Owner,
            DynamicVars.Cards.IntValue,
            c => c.Rarity == CardRarity.Rare,
            rng: Owner.PlayerRng.NonCombatGeneration);
    }
}
