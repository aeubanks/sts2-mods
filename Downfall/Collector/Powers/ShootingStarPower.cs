using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Powers;

/// <summary>
/// Shooting Star (ShootingStarPower): The first Amount time(s) you Pyre an Attack each turn,
/// add a copy of it into your hand. It costs 0 until played.
/// </summary>
public sealed class ShootingStarPower : CollectorPower, IOnPyreListener
{
    private int _pyresThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.PyreHoverTip
    ];

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner.Player)
            _pyresThisTurn = 0;
        return Task.CompletedTask;
    }

    public async Task OnPyreAsync(PlayerChoiceContext choiceContext, Player player, CardModel pyredCard)
    {
        if (player != Owner.Player
            || pyredCard.Type != CardType.Attack
            || _pyresThisTurn >= Amount
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _pyresThisTurn++;
        Flash();
        var copy = pyredCard.CreateClone();
        if (!copy.EnergyCost.CostsX)
            copy.EnergyCost.SetUntilPlayed(0);
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, player);
    }
}
