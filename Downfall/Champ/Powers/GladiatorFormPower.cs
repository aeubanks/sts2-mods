using Downfall.Champ.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Champ.Powers;

public sealed class GladiatorFormPower : ChampPower
{
    private int _vigorRemainder;
    private int _counterRemainder;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VigorPower>(),
        HoverTipFactory.FromPower<CounterPower>()
    ];

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _vigorRemainder = 0;
            _counterRemainder = 0;
        }
        return Task.CompletedTask;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner
            || cardPlay.Card.Type != CardType.Attack
            || cardPlay.Card is CrookedStrike
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int vigor = Owner.GetPowerAmount<VigorPower>();
        if (vigor <= 0)
            return;

        int total = vigor + _vigorRemainder;
        int triggers = total / 3;
        _vigorRemainder = total % 3;

        if (triggers > 0)
        {
            Flash();
            await PowerCmd.Apply<NextTurnVigorPower>(
                new ThrowingPlayerChoiceContext(),
                Owner,
                triggers * Amount,
                Owner,
                null);
        }
    }

    internal async Task OnCounterSpentAsync(PlayerChoiceContext choiceContext, int counterSpent)
    {
        if (counterSpent <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int total = counterSpent + _counterRemainder;
        int triggers = total / 3;
        _counterRemainder = total % 3;

        if (triggers > 0)
        {
            Flash();
            await PowerCmd.Apply<NextTurnCounterPower>(
                choiceContext,
                Owner,
                triggers * Amount,
                Owner,
                null);
        }
    }
}
