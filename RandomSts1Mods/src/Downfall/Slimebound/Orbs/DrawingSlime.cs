using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Ancient Slime: deals 3 (+Potency) damage and draws 1 card (Draw does not scale with Potency).
/// </summary>
public sealed class DrawingSlime : SlimeOrb
{
    public override int BaseDamage => 3;
    public override int BaseSecondary => 1;
    public override bool SecondaryScalesWithPotency => false;
    public override Color DarkenedColor => new("#5C7A7A");

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        ActivatePassive();
        var hitTarget = target is { IsAlive: true } ? target : PickRandomAliveEnemy();
        if (hitTarget is not null)
        {
            await DealUnpoweredDamageAsync(choiceContext, hitTarget, PassiveVal);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, 1m, Owner);
        }
    }
}
