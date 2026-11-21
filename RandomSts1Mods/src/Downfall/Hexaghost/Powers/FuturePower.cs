using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Into Shadow (FuturePower): Whenever you Retract, your next Amount card(s) cost 0 and Exhaust.
/// </summary>
public sealed class FuturePower : HexaPower, IGhostflameRetractListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.RetractHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    public async Task OnGhostflameRetractedAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || Amount <= 0
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<PlayCardAndExhaustPower>(choiceContext, Owner, Amount, Owner, null);
    }
}

/// <summary>
/// Shadowy Shift (PlayCardAndExhaustPower): Your next Amount non-autoplay card(s) cost 0 and Exhaust.
/// </summary>
public sealed class PlayCardAndExhaustPower : HexaPower
{
    private HashSet<CardModel>? _consumingCards;
    private HashSet<CardModel> ConsumingCards => _consumingCards ??= [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _consumingCards = null;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        int activeCount = _consumingCards?.Count ?? 0;
        if (card.Owner == Owner.Player
            && !card.EnergyCost.CostsX
            && Amount > activeCount)
        {
            modifiedCost = 0m;
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        bool alreadyConsuming = _consumingCards?.Contains(card) == true;
        int activeCount = _consumingCards?.Count ?? 0;
        if (card.Owner == Owner.Player
            && !isAutoPlay
            && !card.EnergyCost.CostsX
            && (alreadyConsuming || Amount > activeCount))
        {
            if (!alreadyConsuming)
                ConsumingCards.Add(card);
            if (card.Type != CardType.Power)
                location.pileType = PileType.Exhaust;
        }

        return location;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.IsAutoPlay
            || !cardPlay.IsLastInSeries
            || _consumingCards is null
            || !_consumingCards.Remove(cardPlay.Card))
        {
            return;
        }

        if (Owner.IsAlive && Owner.Powers.Contains(this) && !CombatManager.Instance.IsOverOrEnding)
        {
            Flash();
            await PowerCmd.Decrement(this);
        }
    }
}
