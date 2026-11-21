using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Hermit.Powers;

public sealed class HighNoonPower : HermitPower
{
    protected override string IconKey => "high_noon_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || !Owner.IsAlive
            || Amount <= 0
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (cardPlay.Card.Tags.Contains(CardTag.Strike) || cardPlay.Card.Tags.Contains(CardTag.Defend))
        {
            Flash();
            await CardPileCmd.Draw(choiceContext, Amount, Owner.Player);
        }
    }
}
