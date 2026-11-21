using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hermit.Powers;

public sealed class ComboPower : HermitPower
{
    private int _returnsUsedThisTurn;

    protected override string IconKey => "combo_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HermitCard.DeadOnHoverTip
    ];

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _returnsUsedThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        if (card is HermitCard { UsesDeadOn: true, IsDeadOnNow: true }
            && card.Owner == Owner.Player
            && location.pileType == PileType.Discard
            && _returnsUsedThisTurn < Amount
            && PileType.Hand.GetPile(card.Owner).Cards.Count < CardPile.MaxCardsInHand)
        {
            location.pileType = PileType.Hand;
            location.position = CardPilePosition.Top;
        }

        return location;
    }

    public override Task AfterModifyingCardPlayResultLocation(CardModel card, CardLocation location)
    {
        if (card.Owner == Owner.Player && location.pileType == PileType.Hand)
        {
            _returnsUsedThisTurn++;
            Flash();
        }
        return Task.CompletedTask;
    }
}
