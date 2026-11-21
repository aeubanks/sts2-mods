using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Cultist Slime: deals 6 (+Potency + UniqueBonus) damage, then permanently increases its damage by 1.
/// </summary>
public sealed class CultistSlime : SlimeOrb
{
    public override int BaseDamage => 6;
    public override Color DarkenedColor => new("#36598F");

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        ActivatePassive();
        var hitTarget = target is { IsAlive: true } ? target : PickRandomAliveEnemy();
        if (hitTarget is null)
            return;

        decimal dmg = PassiveVal;
        await DealUnpoweredDamageAsync(choiceContext, hitTarget, dmg);
        UniqueDamageBonus += 1;
        RefreshVisuals();
    }
}
