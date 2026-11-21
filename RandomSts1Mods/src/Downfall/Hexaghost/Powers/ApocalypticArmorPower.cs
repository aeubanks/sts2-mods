using Downfall.Hexaghost.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Doomsday (ApocalypticArmorPower): The next time at least Amount Ghostflames are Ignited,
/// consume this power and take an extra turn via HexaTimeStopPower.
/// </summary>
public sealed class ApocalypticArmorPower : HexaPower, IGhostflameIgniteListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.IgniteHoverTip,
        HexaCard.GhostflameHoverTip
    ];

    public async Task OnGhostflameIgnitedAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        GhostflameOrb ghostflame,
        int ignitedCount)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || !Owner.Powers.Contains(this)
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (ignitedCount >= Amount)
        {
            Flash();
            await PowerCmd.Remove(this);
            if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await PowerCmd.Apply<HexaTimeStopPower>(choiceContext, Owner, 1m, Owner, null);
        }
    }
}

/// <summary>
/// Doom's Arrival (HexaTimeStopPower): At the end of your turn, immediately take another turn
/// via StS 2's native ShouldTakeExtraTurn / AfterTakingExtraTurn hooks.
/// </summary>
public sealed class HexaTimeStopPower : HexaPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldTakeExtraTurn(Player player)
    {
        return player == Owner.Player && Amount > 0;
    }

    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (player != Owner.Player || Amount <= 0 || !Owner.Powers.Contains(this))
            return;

        Flash();
        await PowerCmd.Decrement(this);
    }
}
