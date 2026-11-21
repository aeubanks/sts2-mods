using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class LastStrike() : SneckoCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override bool IsGift => true;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new CalculationBaseVar(1m),
        new CalculationExtraVar(1m),
        new CalculatedVar("Hits").WithMultiplier((card, _) => CountUniqueStrikesThisCombat(card))
    ];

    private static decimal CountUniqueStrikesThisCombat(CardModel card, CardPlay? excludePlay = null)
    {
        if (card.CombatState is null || card.Owner is null)
            return 0m;

        var uniqueIds = new HashSet<ModelId>();
        foreach (var entry in CombatManager.Instance.History.CardPlaysStarted)
        {
            if (entry.CardPlay != excludePlay
                && entry.CardPlay.Card.Owner == card.Owner
                && entry.CardPlay.Card.Tags.Contains(CardTag.Strike))
            {
                uniqueIds.Add(entry.CardPlay.Card.Id);
            }
        }
        return uniqueIds.Count;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = 1 + (int)CountUniqueStrikesThisCombat(this, cardPlay);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override Task OnGiftClaimedAsync() =>
        SneckoOffclass.OfferOffclassDeckChoiceAsync(
            Owner,
            3,
            c => c.Tags.Contains(CardTag.Strike));

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
