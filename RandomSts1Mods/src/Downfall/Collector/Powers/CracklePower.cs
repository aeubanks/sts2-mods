using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Collector.Powers;

/// <summary>
/// Crackle (CracklePower, applied by Pyromancy): At the start of your turn, gain Amount Reserve(s).
/// </summary>
public sealed class CracklePower : CollectorPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.ReservesHoverTip
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await CollectorMechanics.GainReservesAsync(choiceContext, player, Amount);
    }
}
