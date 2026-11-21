using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Torch Head Slime: deals 6 (+Potency + Strength) damage to a random enemy.
/// </summary>
public sealed class TorchHeadSlime : SlimeOrb
{
    public override int BaseDamage => 6;
    public override Color DarkenedColor => new("#3A8A68");

    protected override int ExtraDamageBonus =>
        IsMutable && Owner?.Creature is not null
            ? Owner.Creature.GetPowerAmount<StrengthPower>()
            : 0;

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (IsMutable && power is StrengthPower && power.Owner == Owner?.Creature)
        {
            RefreshVisuals();
        }
        return Task.CompletedTask;
    }

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
    }
}
