using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Leeching Slime: deals 1 (+Potency) damage and grants 3 (+ceil(Potency/2)) Block at the start of your turn.
/// </summary>
public sealed class ShieldSlime : SlimeOrb
{
    public override int BaseDamage => 1;
    public override int BaseSecondary => 3;
    public override Color DarkenedColor => new("#1E5D8A");

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block),
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override async Task Passive(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        ActivatePassive();
        decimal block = EvokeVal;
        if (block > 0m)
        {
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Unpowered, null);
        }

        var hitTarget = target is { IsAlive: true } ? target : PickRandomAliveEnemy();
        if (hitTarget is not null)
        {
            await DealUnpoweredDamageAsync(choiceContext, hitTarget, PassiveVal);
        }
    }
}
