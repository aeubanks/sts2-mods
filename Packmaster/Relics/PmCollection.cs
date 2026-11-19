using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;

namespace Packmaster.Relics;

public sealed class PmCollection : PackmasterRelic
{
    protected override string IconKey => "pm_collection";

    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        Flash();
        await PackRelicHelper.OfferRandomPackCardsToDeckAsync(
            Owner,
            new LocString("relics", "PACKMASTER-PM_COLLECTION.selectionScreenPrompt"),
            minSelect: 0,
            maxSelect: int.MaxValue);
    }
}
