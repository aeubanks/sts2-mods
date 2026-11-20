using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Slimebound.Powers;

public sealed class CommandOnPlayPower : SlimePower
{
    private CardModel? _skipCardSource;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.CommandHoverTip
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

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, _skipCardSource))
        {
            _skipCardSource = null;
            return;
        }

        if (Amount <= 0
            || cardPlay.Card.Owner?.Creature != Owner
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Player is not { } player)
        {
            return;
        }

        Flash();
        await PowerCmd.Decrement(this);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await SlimeMechanics.CommandAsync(choiceContext, player);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && Amount > 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
