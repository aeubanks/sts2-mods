using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class Belittle() : SneckoCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool IsGift => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("HpLoss", 9m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int debuffs = SneckoMechanics.CountDebuffs(cardPlay.Target);
        int perDebuff = DynamicVars["HpLoss"].IntValue;
        for (int i = 0; i < debuffs; i++)
        {
            if (!cardPlay.Target.IsAlive)
                break;
            await CreatureCmd.Damage(
                choiceContext,
                cardPlay.Target,
                perDebuff,
                ValueProp.Unblockable | ValueProp.Unpowered,
                this,
                cardPlay);
        }
    }

    protected override Task OnGiftClaimedAsync() =>
        SneckoOffclass.OfferOffclassDeckChoiceAsync(
            Owner,
            3,
            c => c.Rarity == CardRarity.Uncommon && SneckoOffclass.IsDebuffCard(c));

    protected override void OnUpgrade()
    {
        DynamicVars["HpLoss"].UpgradeValueBy(3m);
    }
}
