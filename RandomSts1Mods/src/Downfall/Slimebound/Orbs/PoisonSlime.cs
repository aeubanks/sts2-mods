using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Guerilla Slime: deals 3 (+Potency) damage to ALL enemies at the start of your turn.
/// </summary>
public sealed class PoisonSlime : SlimeOrb
{
    public override int BaseDamage => 3;
    public override Color DarkenedColor => new("#4D7530");

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        ActivatePassive();
        await DealUnpoweredAoeDamageAsync(choiceContext, PassiveVal);
    }
}
