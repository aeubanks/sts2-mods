using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Bruiser Slime: deals 3 (+Potency) damage twice to a random enemy at the start of your turn.
/// </summary>
public sealed class AttackSlime : SlimeOrb
{
    public override int BaseDamage => 3;
    public override Color DarkenedColor => new("#7A3E3E");

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        decimal dmg = PassiveVal;
        for (int i = 0; i < 2; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;

            var hitTarget = target is { IsAlive: true } ? target : PickRandomAliveEnemy();
            if (hitTarget is null)
                break;

            ActivatePassive();
            await DealUnpoweredDamageAsync(choiceContext, hitTarget, dmg);
        }
    }
}
