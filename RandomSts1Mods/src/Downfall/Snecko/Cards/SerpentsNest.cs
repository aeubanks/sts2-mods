using Downfall.Snecko.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Snecko.Cards;

public sealed class SerpentsNest() : SneckoCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    public override bool IsGift => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SerpentsNestPower>(7m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SerpentsNestPower>(choiceContext, Owner.Creature, DynamicVars["SerpentsNestPower"].BaseValue, Owner.Creature, this);
    }

    protected override Task OnGiftClaimedAsync() =>
        SneckoOffclass.OfferOffclassDeckChoiceAsync(
            Owner,
            3,
            c => c.Type == CardType.Power && c.Rarity == CardRarity.Uncommon);

    protected override void OnUpgrade()
    {
        DynamicVars["SerpentsNestPower"].UpgradeValueBy(3m);
    }
}
