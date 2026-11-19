using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Gremlins.Powers;

public sealed class CongaLinePower : GremlinPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GremlinCard.SwapHoverTip
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Amount <= 0
            || Owner.CombatState is not { } combatState
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(Amount, player);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GremlinMob.SwapNextAsync(player, combatState, choiceContext);
        }
    }
}
