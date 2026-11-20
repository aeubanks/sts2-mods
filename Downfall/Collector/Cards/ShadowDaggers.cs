using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Shadow Daggers (Uncommon, 1 cost, Attack, Exhaust):
/// Deal 3 (5) damage for each Collected card played this combat.
/// </summary>
public sealed class ShadowDaggers() : CollectorCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar("HitCount").WithMultiplier((card, _) =>
        {
            if (card.CombatState is null || card.Owner is null)
                return 0m;
            return CollectorCombatState.Get(card.CombatState).GetPlayer(card.Owner).CollectedCardsPlayedThisCombat;
        })
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectedHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null)
            return;

        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        int hits = CollectorCombatState.Get(CombatState).GetPlayer(Owner).CollectedCardsPlayedThisCombat;
        if (hits <= 0)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
