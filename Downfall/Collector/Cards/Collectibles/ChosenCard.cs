using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Cards.Collectibles;

/// <summary>
/// Chosen (Collectible, 1 cost, Skill, Exhaust):
/// Apply 2 (3) Weak. If the enemy does not intend to attack, apply 3 (5) Vulnerable.
/// </summary>
public sealed class ChosenCard() : CollectibleCard(1, CardType.Skill, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(2m),
        new PowerVar<VulnerablePower>(3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override bool ShouldGlowGoldInternal =>
        CombatState?.HittableEnemies.Any(e => e.IsAlive && e.Monster is { IntendsToAttack: false }) == true;

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.BaseValue, Owner.Creature, this);
        if (target.IsAlive
            && target.Monster is { IntendsToAttack: false }
            && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Weak.UpgradeValueBy(1m);
        DynamicVars.Vulnerable.UpgradeValueBy(2m);
    }
}
