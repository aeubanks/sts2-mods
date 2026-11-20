using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class ComboString() : SneckoCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool IsGift => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar("Hits").WithMultiplier((card, _) => CountOffclassPlayedThisTurn(card))
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        OffclassHoverTip,
        GiftHoverTip
    ];

    private static decimal CountOffclassPlayedThisTurn(MegaCrit.Sts2.Core.Models.CardModel card, CardPlay? excludePlay = null)
    {
        if (card.CombatState is null || card.Owner is null)
            return 0m;

        int count = 0;
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.CardPlay != excludePlay
                && entry.HappenedThisTurn(card.CombatState)
                && entry.CardPlay.Card.Owner == card.Owner
                && SneckoOffclass.IsOffclass(entry.CardPlay.Card, card.Owner))
            {
                count++;
            }
        }
        return count;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = (int)CountOffclassPlayedThisTurn(this, cardPlay);
        if (hits <= 0)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override Task OnGiftClaimedAsync() =>
        SneckoOffclass.OfferOffclassDeckChoiceAsync(Owner, 3, c => c.Rarity == CardRarity.Uncommon);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
