using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Mire Slime: deals 2 (+Potency) damage and applies 2 (+ceil(Potency/2) + Acid Tongue) Goop at the start of your turn.
/// </summary>
public sealed class SlimingSlime : SlimeOrb
{
    public override int BaseDamage => 2;
    public override int BaseSecondary => 2;
    public override Color DarkenedColor => new("#693D75");

    protected override int ExtraSecondaryBonus =>
        IsMutable && Owner is not null
            ? SlimeMechanics.GetAcidTongueBonus(Owner)
            : 0;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.GoopHoverTip,
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

        int baseGoop = BaseSecondary + (int)Math.Ceiling(PotencyAmount / 2.0);
        await DealUnpoweredDamageAsync(choiceContext, hitTarget, PassiveVal);
        if (hitTarget.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await SlimeMechanics.ApplyGoopAsync(choiceContext, hitTarget, Owner, baseGoop, null);
        }
    }
}
