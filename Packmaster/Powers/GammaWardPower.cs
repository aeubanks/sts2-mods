using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using Packmaster.Cards.EvenOdd;

namespace Packmaster.Powers;

public sealed class GammaWardPower : PackmasterPower
{
    protected override string IconKey => "gamma_ward_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner
            || cardPlay.Card.Type != CardType.Skill
            || !Owner.IsAlive)
        {
            return;
        }

        decimal blockAmount = Amount;
        bool triggerBlock = EvenOddHelper.IsNextPlayEven(Owner.Player, Owner.CombatState);
        if (triggerBlock)
        {
            Flash();
        }

        await PowerCmd.Remove(this);

        if (triggerBlock && Owner.IsAlive)
        {
            await CreatureCmd.GainBlock(Owner, blockAmount, ValueProp.Move, cardPlay);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }
}
