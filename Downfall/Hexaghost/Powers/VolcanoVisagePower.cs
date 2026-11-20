using Downfall.Hexaghost.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Volcanic Visage (VolcanoVisagePower): Whenever you Ignite a Ghostflame,
/// apply Amount Soulburn to ALL enemies.
/// </summary>
public sealed class VolcanoVisagePower : HexaPower, IGhostflameIgniteListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.IgniteHoverTip,
        HexaCard.SoulburnHoverTip
    ];

    public async Task OnGhostflameIgnitedAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        GhostflameOrb ghostflame,
        int ignitedCount)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || Amount <= 0
            || Owner.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        foreach (var enemy in combatState.HittableEnemies.Where(e => e.IsAlive).ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!enemy.IsAlive)
                continue;
            await HexaMechanics.ApplySoulburnAsync(choiceContext, enemy, Amount, Owner);
        }
    }
}
