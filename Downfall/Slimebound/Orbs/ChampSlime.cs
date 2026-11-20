using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Slimebound.Orbs;

/// <summary>
/// Insulting Slime: deals 4 (+Potency) damage and applies 1 (+ceil(Potency/2)) Vulnerable.
/// </summary>
public sealed class ChampSlime : SlimeOrb
{
    public override int BaseDamage => 4;
    public override int BaseSecondary => 1;
    public override Color DarkenedColor => new("#9A4A4A");

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
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

        decimal vuln = EvokeVal;
        await DealUnpoweredDamageAsync(choiceContext, hitTarget, PassiveVal);
        if (vuln > 0m && hitTarget.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, hitTarget, vuln, Owner.Creature, null);
        }
    }
}
