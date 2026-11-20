using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Potions;

/// <summary>
/// Soul Extract (MiniCursePotion, Common):
/// Apply 1 Weak, 1 Vulnerable, and 6 Doom.
/// </summary>
public sealed class MiniCursePotion : CollectorPotion
{
    public override PotionRarity Rarity => PotionRarity.Common;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(1m),
        new PowerVar<VulnerablePower>(1m),
        new PowerVar<CollectorDoomPower>(6m)
    ];

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>(),
        CollectorCard.DoomHoverTip
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var resolved = CollectorCard.ResolveTarget(Owner, target);
        if (resolved is null || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<WeakPower>(choiceContext, resolved, DynamicVars.Weak.BaseValue, Owner.Creature, null);
        if (resolved.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<VulnerablePower>(choiceContext, resolved, DynamicVars.Vulnerable.BaseValue, Owner.Creature, null);
        if (resolved.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await CollectorMechanics.ApplyDoomAsync(choiceContext, resolved, DynamicVars["CollectorDoomPower"].BaseValue, Owner.Creature, null);
    }
}
