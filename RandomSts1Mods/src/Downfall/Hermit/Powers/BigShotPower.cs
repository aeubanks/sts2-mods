using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Hermit.Powers;

public sealed class BigShotPower : HermitPower
{
    protected override string IconKey => "big_shot_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HermitCard.DeadOnHoverTip,
        HoverTipFactory.FromPower<VigorPower>()
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || !Owner.IsAlive
            || Amount <= 0
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int triggers = HermitCard.GetDeadOnTriggerCount(CombatState, cardPlay);
        if (triggers <= 0)
            return;

        Flash();
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner, Amount * triggers, Owner, null);
    }
}
