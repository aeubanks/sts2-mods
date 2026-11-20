using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Time Slime: deals 4 (+Potency) damage and applies 1 (+ceil(Potency/2)) Weak.
/// </summary>
public sealed class SlowingSlime : SlimeOrb
{
    public override int BaseDamage => 4;
    public override int BaseSecondary => 1;
    public override Color DarkenedColor => new("#4D8C75");

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        ActivatePassive();
        var hitTarget = target is { IsAlive: true } ? target : PickRandomAliveEnemy();
        if (hitTarget is null)
            return;

        decimal weak = EvokeVal;
        await DealUnpoweredDamageAsync(choiceContext, hitTarget, PassiveVal);
        if (weak > 0m && hitTarget.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, hitTarget, weak, Owner.Creature, null);
        }
    }
}
