using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Packmaster.Cards.Farmer;

namespace Packmaster.Powers;

public sealed class SupplyChainPower : PackmasterPower
{
    protected override string IconKey => "supply_chain_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Fertilizer>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || Owner.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
            return;

        int count = Amount;
        if (count <= 0)
            return;

        Flash();
        var fertilizers = Enumerable.Range(0, count)
            .Select(_ => combatState.CreateCard<Fertilizer>(player))
            .ToList();
        await CardPileCmd.AddGeneratedCardsToCombat(fertilizers, PileType.Hand, player);
    }
}
