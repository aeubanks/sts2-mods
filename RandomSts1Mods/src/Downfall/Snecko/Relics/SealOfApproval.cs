using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Snecko.Relics;

public sealed class SealOfApproval : SneckoRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(5)
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
            c => c.Type == CardType.Power && c.Rarity == CardRarity.Uncommon,
            rng: Owner.PlayerRng.NonCombatGeneration,
            selectionPrompt: new LocString("relics", "DOWNFALL-SEAL_OF_APPROVAL.selectionScreenPrompt"));
    }
}
