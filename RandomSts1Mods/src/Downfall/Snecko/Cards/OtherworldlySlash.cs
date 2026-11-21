using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class OtherworldlySlash() : SneckoCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override bool IsGift => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        OffclassHoverTip,
        GiftHoverTip
    ];

    private bool PlayedOffclassThisTurn(CardPlay? excludePlay = null)
    {
        if (CombatState is null || Owner is null)
            return false;

        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.CardPlay != excludePlay
                && entry.HappenedThisTurn(CombatState)
                && entry.CardPlay.Card.Owner == Owner
                && SneckoOffclass.IsOffclass(entry.CardPlay.Card, Owner))
            {
                return true;
            }
        }
        return false;
    }

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && Pile?.Type == PileType.Hand && PlayedOffclassThisTurn();

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = PlayedOffclassThisTurn(cardPlay) ? 2 : 1;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override Task OnGiftClaimedAsync() =>
        SneckoOffclass.OfferOffclassDeckChoiceAsync(Owner, 3, c => c.Rarity == CardRarity.Common);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
