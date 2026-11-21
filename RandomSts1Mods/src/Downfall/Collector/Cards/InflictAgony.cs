using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Inflict Agony (Uncommon, 2 cost, Attack):
/// Deal 12 (18) damage. If the enemy is not Afflicted, apply 2 Weak and 2 Vulnerable.
/// </summary>
public sealed class InflictAgony() : CollectorCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && CombatState.HittableEnemies.Any(e => e.IsAlive && !CollectorMechanics.IsAfflicted(e));

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AfflictedHoverTip,
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding && !CollectorMechanics.IsAfflicted(target))
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, target, 2m, Owner.Creature, this);
            if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await PowerCmd.Apply<VulnerablePower>(choiceContext, target, 2m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
    }
}
