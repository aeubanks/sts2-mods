using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Packmaster.Cards.EvenOdd;

namespace Packmaster.Powers;

public sealed class PrimeDirectivePower : PackmasterPower
{
    protected override string IconKey => "prime_directive_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer != Owner
            || !props.IsPoweredAttack()
            || cardSource is null
            || cardSource.Type != CardType.Attack
            || Owner.Player is null)
        {
            return 0m;
        }

        bool isPlaying = cardPlay is not null || cardSource.Pile?.Type == PileType.Play;
        bool isOdd = isPlaying
            ? EvenOddHelper.IsCurrentPlayOdd(Owner.Player, Owner.CombatState)
            : EvenOddHelper.IsNextPlayOdd(Owner.Player, Owner.CombatState);

        return isOdd ? Amount : 0m;
    }
}
