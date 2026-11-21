using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Guardian.Powers;

public sealed class ClonePower : GuardianPower
{
    private CardModel? _skipCardSource;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.StasisHoverTip
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _skipCardSource = null;
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (cardSource is not null)
        {
            _skipCardSource = cardSource;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (oldPileType != PileType.Play)
            return;

        if (ReferenceEquals(card, _skipCardSource))
        {
            _skipCardSource = null;
            return;
        }

        if (Amount <= 0
            || card.Owner?.Creature != Owner
            || card.Type is not (CardType.Attack or CardType.Skill)
            || card is GuardianCard { IsSelfStasis: true }
            || card.Pile?.Type != PileType.Discard
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Player is not { } player
            || !GuardianMechanics.CanPlaceInStasis(player))
        {
            return;
        }

        Flash();
        await PowerCmd.Decrement(this);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.PlaceInStasisAsync(new ThrowingPlayerChoiceContext(), player, card, isSelfStasis: true);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Side)
        {
            _skipCardSource = null;
            if (Amount > 0 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await PowerCmd.Remove(this);
            }
        }
    }
}
