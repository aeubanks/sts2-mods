using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Awakened.Powers;

public sealed class ArchmagusPower : AwakenedPower
{
    protected override string IconKey => "archmagus_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner.Creature != Owner
            || card is not AwakenedSpellCard
            || CombatState is null)
        {
            return playCount;
        }

        int spellsStartedThisTurn = CombatManager.Instance.History.CardPlaysStarted.Count(e =>
            e.HappenedThisTurn(CombatState)
            && e.CardPlay.Card.Owner == Owner.Player
            && e.CardPlay.Card is AwakenedSpellCard
            && e.CardPlay.IsFirstInSeries);

        if (spellsStartedThisTurn >= Amount)
            return playCount;

        return playCount + 1;
    }

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (card.Owner.Creature == Owner && card is AwakenedSpellCard)
        {
            Flash();
        }
        return Task.CompletedTask;
    }
}
