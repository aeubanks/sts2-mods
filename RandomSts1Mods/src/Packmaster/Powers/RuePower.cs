using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Powers;

public sealed class RuePower : PackmasterPower
{
    private readonly HashSet<CardModel> _armedCards = new(ReferenceEqualityComparer.Instance);

    protected override string IconKey => "rue_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        if (Owner.Player is not null
            && card.Owner == Owner.Player
            && Owner.IsAlive
            && (_armedCards.Contains(card) || _armedCards.Count < Amount)
            && !CombatManager.Instance.IsOverOrEnding)
        {
            _armedCards.Add(card);
            if (card.Type != CardType.Power && location.pileType != PileType.None)
            {
                location.pileType = PileType.Exhaust;
            }
        }

        return location;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.PlayIndex != 0 || !_armedCards.Remove(cardPlay.Card))
            return;

        if (Owner.IsAlive && Amount > 0 && !CombatManager.Instance.IsOverOrEnding)
        {
            Flash();
            await PowerCmd.Decrement(this);
        }
    }
}
