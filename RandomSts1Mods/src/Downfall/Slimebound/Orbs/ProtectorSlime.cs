using Downfall.Slimebound.Powers;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Spiky Slime: deals 4 (+Potency) damage and grants 4 (+ceil(Potency/2)) temporary Thorns.
/// </summary>
public sealed class ProtectorSlime : SlimeOrb
{
    public override int BaseDamage => 4;
    public override int BaseSecondary => 4;
    public override Color DarkenedColor => new("#8C825C");

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ThornsPower>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        ActivatePassive();
        decimal thorns = EvokeVal;
        if (thorns > 0m)
        {
            await PowerCmd.Apply<SlimeTempThornsPower>(choiceContext, Owner.Creature, thorns, Owner.Creature, null);
        }

        var hitTarget = target is { IsAlive: true } ? target : PickRandomAliveEnemy();
        if (hitTarget is not null)
        {
            await DealUnpoweredDamageAsync(choiceContext, hitTarget, PassiveVal);
        }
    }
}
