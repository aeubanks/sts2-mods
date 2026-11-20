using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// I'll Take That (IllTakeThat, Uncommon, 1 cost, Attack):
/// Steal up to 10 (13) of the enemy's Block. Deal 10 (13) damage.
/// </summary>
public sealed class IllTakeThat() : CollectorCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null && CombatState.HittableEnemies.Any(e => e.IsAlive && e.Block > 0);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new IntVar("StealBlock", 10m)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        if (target.Block > 0)
        {
            int toSteal = Math.Min(DynamicVars["StealBlock"].IntValue, target.Block);
            if (toSteal > 0)
            {
                await CreatureCmd.LoseBlock(choiceContext, target, toSteal, Owner.Creature);
                if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                    await CreatureCmd.GainBlock(Owner.Creature, toSteal, ValueProp.Unpowered, null);
            }
        }

        if (target.IsAlive && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["StealBlock"].UpgradeValueBy(3m);
    }
}
