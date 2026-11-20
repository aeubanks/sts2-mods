using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class SlitherThrough() : SneckoCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool IsGift => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(14m, ValueProp.Move),
        new IntVar("CostReduction", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        OffclassHoverTip,
        GiftHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        int reduction = DynamicVars["CostReduction"].IntValue;
        foreach (var card in PileType.Hand.GetPile(Owner).Cards)
        {
            if (SneckoOffclass.IsOffclass(card, Owner)
                && SneckoMechanics.CanMuddle(card)
                && card.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            {
                card.EnergyCost.AddThisTurn(-reduction, reduceOnly: true);
            }
        }
    }

    protected override Task OnGiftClaimedAsync() =>
        SneckoOffclass.OfferOffclassDeckChoiceAsync(Owner, 3, c => c.Rarity == CardRarity.Uncommon);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
