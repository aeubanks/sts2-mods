using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Cards;

/// <summary>
/// Mini-Curse (MiniCurse, Uncommon, 0 cost, Skill, Pyre):
/// Apply 1 Weak and 1 Vulnerable (Upgraded: to ALL enemies).
/// </summary>
public sealed class MiniCurse() : CollectorCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool IsPyre => true;

    public override TargetType TargetType => IsUpgraded ? TargetType.AllEnemies : TargetType.AnyEnemy;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        PyreHoverTip,
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (IsUpgraded)
        {
            foreach (var enemy in CombatState.HittableEnemies.Where(e => e.IsAlive).ToList())
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;
                if (!enemy.IsAlive)
                    continue;

                await PowerCmd.Apply<WeakPower>(choiceContext, enemy, 1m, Owner.Creature, this);
                if (enemy.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                    await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy, 1m, Owner.Creature, this);
            }
        }
        else
        {
            var target = ResolveTarget(Owner, cardPlay.Target);
            if (target is not null)
            {
                await PowerCmd.Apply<WeakPower>(choiceContext, target, 1m, Owner.Creature, this);
                if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                    await PowerCmd.Apply<VulnerablePower>(choiceContext, target, 1m, Owner.Creature, this);
            }
        }
    }
}
