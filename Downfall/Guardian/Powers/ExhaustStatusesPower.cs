using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Guardian.Powers;

public sealed class ExhaustStatusesPower : GuardianPower
{
    private int _usedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _usedThisTurn = 0;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
            _usedThisTurn = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (Amount <= 0
            || _usedThisTurn >= Amount
            || card.Owner?.Creature != Owner
            || card.Type is not (CardType.Status or CardType.Curse)
            || card.Pile?.Type != PileType.Hand
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Player is not { } player)
        {
            return;
        }

        _usedThisTurn++;
        Flash();
        await CardCmd.Exhaust(choiceContext, card);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, 1, player);
        }
    }
}
