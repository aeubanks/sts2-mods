using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Snecko.Cards;

public sealed class GlitteringGambit() : SneckoCard(-1, CardType.Power, CardRarity.Rare, TargetType.None)
{
    public override bool IsGift => true;
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Unplayable,
        CardKeyword.Eternal
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new GoldVar(150)
    ];

    protected override async Task OnGiftClaimedAsync()
    {
        await PlayerCmd.GainGold(DynamicVars.Gold.IntValue, Owner);
        await SneckoOffclass.OfferOffclassDeckChoiceAsync(
            Owner,
            3,
            c => c.Rarity == CardRarity.Rare,
            upgradeAll: true);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Ethereal);
    }
}
