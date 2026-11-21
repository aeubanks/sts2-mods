using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class ToothAndClaw() : SneckoCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool IsGift => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Shiv>(IsUpgraded),
        GiftHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_bite")
            .Execute(choiceContext);

        if (CombatState is null)
            return;

        int suits = SneckoOffclass.CountUniqueSuitsInHand(Owner, includeCard: this);
        for (int i = 0; i < suits; i++)
        {
            var shiv = CombatState.CreateCard<Shiv>(Owner);
            if (IsUpgraded)
            {
                CardCmd.Upgrade(shiv);
            }
            await CardPileCmd.AddGeneratedCardToCombat(shiv, PileType.Hand, Owner);
        }
    }

    protected override Task OnGiftClaimedAsync() =>
        SneckoOffclass.OfferOffclassDeckChoiceAsync(Owner, 3, c => c.Rarity == CardRarity.Uncommon);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
