using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Ghostflame Slime: deals 4 (+Potency) damage and applies 6 (+ceil(Potency/2)) Soulburn.
/// </summary>
public sealed class HexSlime : SlimeOrb
{
    public override int BaseDamage => 4;
    public override int BaseSecondary => 6;
    public override Color DarkenedColor => new("#4A6E9E");

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<SoulburnPower>(),
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

        decimal soulburn = EvokeVal;
        await DealUnpoweredDamageAsync(choiceContext, hitTarget, PassiveVal);
        if (soulburn > 0m && hitTarget.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<SoulburnPower>(choiceContext, hitTarget, soulburn, Owner.Creature, null);
        }
    }
}
