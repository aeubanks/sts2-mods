using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Snecko.Powers;

public sealed class CheapStockPower : SneckoPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.MuddleHoverTip
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await SneckoMechanics.MuddleHighestCostInHandAsync(choiceContext, player, Amount);
    }
}
