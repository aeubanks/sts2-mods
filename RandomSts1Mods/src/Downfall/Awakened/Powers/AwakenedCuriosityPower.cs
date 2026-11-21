using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Powers;

public sealed class AwakenedCuriosityPower : AwakenedPower
{
    private CardPlay? _ignoredPlay;
    private int _ignoredAmount;

    protected override string IconKey => "awakened_curiosity_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _ignoredPlay = null;
        _ignoredAmount = 0;
    }

    internal void IgnoreAmountForPlay(CardPlay cardPlay, int addedAmount)
    {
        _ignoredPlay = cardPlay;
        _ignoredAmount = addedAmount;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int effectiveAmount = Amount;
        if (ReferenceEquals(cardPlay, _ignoredPlay))
        {
            effectiveAmount = Amount - _ignoredAmount;
            _ignoredPlay = null;
            _ignoredAmount = 0;
        }

        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.Card.Type != CardType.Power
            || effectiveAmount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, effectiveAmount, Owner, null);
    }
}
