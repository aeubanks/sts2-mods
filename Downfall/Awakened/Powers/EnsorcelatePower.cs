using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Awakened.Powers;

public sealed class EnsorcelatePower : AwakenedPower
{
    private HashSet<CardPlay>? _consumingPlays;
    private HashSet<CardPlay> ConsumingPlays => _consumingPlays ??= [];

    protected override string IconKey => "ensorcelate_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _consumingPlays = null;
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        int activeCount = _consumingPlays?.Count ?? 0;
        if (card.Owner == Owner.Player
            && card.Type == CardType.Power
            && Amount > activeCount)
        {
            modifiedCost = 0m;
            return true;
        }

        modifiedCost = originalCost;
        return false;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        int activeCount = _consumingPlays?.Count ?? 0;
        if (cardPlay.Card.Owner == Owner.Player
            && cardPlay.Card.Type == CardType.Power
            && cardPlay.PlayIndex == 0
            && !cardPlay.IsAutoPlay
            && Amount > activeCount)
        {
            ConsumingPlays.Add(cardPlay);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_consumingPlays is not null
            && _consumingPlays.Remove(cardPlay)
            && Owner.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
        {
            Flash();
            await PowerCmd.Decrement(this);
        }
    }
}
