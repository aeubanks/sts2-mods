using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Powers;

public sealed class SneakyGremlinPower : GremlinActivePower
{
    private HashSet<CardPlay>? _armedAttacks;

    public override GremlinKind Kind => GremlinKind.Sneaky;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _armedAttacks = null;
    }

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
            || cardSource is not { Type: CardType.Attack }
            || cardSource.EnergyCost.CostsX
            || cardSource.EnergyCost.GetWithModifiers(CostModifiers.All) != 0
            || Amount <= 0)
        {
            return 0m;
        }

        return Amount;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player && cardPlay.Card.Type == CardType.Attack)
        {
            (_armedAttacks ??= []).Add(cardPlay);
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_armedAttacks is null
            || !_armedAttacks.Remove(cardPlay)
            || Amount <= 0
            || Owner.Player is not { } player
            || Owner.CombatState is not { } combatState
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await GremlinMob.DealRandomUnpoweredDamageAsync(choiceContext, player, combatState, Amount);
    }
}
