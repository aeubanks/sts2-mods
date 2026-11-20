using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Cards;

/// <summary>
/// You Are Mine! (Basic, 2 cost): Apply 1 Weak, 1 Vulnerable, and 6 (8) Doom.
/// </summary>
public sealed class YouAreMine() : CollectorCard(2, CardType.Skill, CardRarity.Basic, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(1m),
        new PowerVar<VulnerablePower>(1m),
        new PowerVar<CollectorDoomPower>(6m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>(),
        DoomHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars.Weak.BaseValue, Owner.Creature, this);
        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await CollectorMechanics.ApplyDoomAsync(choiceContext, target, DynamicVars[nameof(CollectorDoomPower)].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(CollectorDoomPower)].UpgradeValueBy(2m);
    }
}
