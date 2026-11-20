using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Spirit Leech (Common, 2 cost, Attack):
/// Deal 13 (15) damage. If the enemy is Afflicted, gain 11 (13) Block.
/// </summary>
public sealed class SpiritLeech() : CollectorCard(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override bool GainsBlock => true;

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && CombatState.HittableEnemies.Any(CollectorMechanics.IsAfflicted);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(13m, ValueProp.Move),
        new BlockVar(11m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AfflictedHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        bool wasAfflicted = CollectorMechanics.IsAfflicted(target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (wasAfflicted && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
