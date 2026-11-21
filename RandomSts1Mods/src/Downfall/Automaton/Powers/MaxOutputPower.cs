using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Max Output (MaxOutputPower): At the start of each turn, draw Amount additional card(s)
/// and Insert Amount Dazed into your Draw Pile.
/// </summary>
public sealed class MaxOutputPower : BronzePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.InsertHoverTip,
        HoverTipFactory.FromCard<Dazed>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await CardPileCmd.Draw(choiceContext, Amount, player);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await AutomatonMechanics.InsertCardIntoDrawPileAsync<Dazed>(player, Amount);
    }
}
