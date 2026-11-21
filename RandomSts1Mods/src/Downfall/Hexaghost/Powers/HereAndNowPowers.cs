using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Here and Now (Energy portion): At the start of your turn, gain Amount Energy.
/// </summary>
public sealed class HexaEnergyPerTurnPower : HexaPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || Amount <= 0
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(Amount, player);
    }
}

/// <summary>
/// Here and Now (Extinguish portion): At the end of your turn, Extinguish the active Ghostflame
/// instead of Advancing.
/// </summary>
public sealed class StopFromAdvancingPower : HexaPower, IStopAdvanceProvider
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.ExtinguishHoverTip,
        HexaCard.GhostflameHoverTip
    ];

    public bool PreventsEndTurnAdvance(Player player) => player == Owner.Player;
}
