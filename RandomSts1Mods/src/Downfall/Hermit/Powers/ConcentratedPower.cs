using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hermit.Powers;

public sealed class ConcentratedPower : HermitPower
{
    private HashSet<CardPlay>? _consumingPlays;
    private HashSet<CardPlay> ConsumingPlays => _consumingPlays ??= [];

    protected override string IconKey => "concentrated_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HermitCard.DeadOnHoverTip
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _consumingPlays = null;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card is HermitCard { UsesDeadOn: true }
            && cardPlay.Card.Owner == Owner.Player)
        {
            ConsumingPlays.Add(cardPlay);
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_consumingPlays is not null
            && _consumingPlays.Remove(cardPlay)
            && cardPlay.PlayIndex >= cardPlay.PlayCount - 1
            && !CombatManager.Instance.IsOverOrEnding)
        {
            Flash();
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Remove(this);
        }
    }
}
