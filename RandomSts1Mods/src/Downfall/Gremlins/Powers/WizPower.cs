using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Powers;

public sealed class WizPower : GremlinPower
{
    public const int Threshold = 3;
    public const decimal BaseBonusDamage = 7m;

    private Dictionary<CardPlay, int>? _armedPlays;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _armedPlays = null;
    }

    public static decimal GetBonusDamage(Creature owner)
    {
        if (owner.GetPowerAmount<WizPower>() < Threshold)
            return 0m;

        decimal bonus = BaseBonusDamage;
        foreach (var p in owner.Powers.OfType<IGremlinWizBonusProvider>())
        {
            bonus += p.BonusWizDamage;
        }
        if (owner.Player is { } player)
        {
            foreach (var r in player.Relics.OfType<IGremlinWizBonusProvider>())
            {
                bonus += r.BonusWizDamage;
            }
        }
        return bonus;
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer != Owner || !props.IsPoweredAttack() || Amount < Threshold)
            return 0m;

        return GetBonusDamage(Owner);
    }

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target != Owner
            || !props.HasFlag(ValueProp.Move)
            || props.HasFlag(ValueProp.Unpowered)
            || cardSource is not GremlinCard { ScalesBlockWithWiz: true }
            || Amount < Threshold)
        {
            return 0m;
        }

        return GetBonusDamage(Owner);
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player
            && Amount >= Threshold
            && cardPlay.Card.Type == CardType.Attack
            && cardPlay.Card is not GremlinCard { ConsumesWiz: false })
        {
            (_armedPlays ??= [])[cardPlay] = Amount;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_armedPlays is null || !_armedPlays.Remove(cardPlay, out int toConsume))
            return;

        await ConsumeAsync(choiceContext, toConsume);
    }

    public async Task ConsumeAsync(PlayerChoiceContext choiceContext, int? amountToConsume = null)
    {
        if (Amount <= 0)
            return;

        int consume = amountToConsume is { } specified ? Math.Min(Amount, specified) : Amount;
        if (consume <= 0)
            return;

        Flash();
        if (consume >= Amount)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.ModifyAmount(choiceContext, this, -consume, null, null);
        }

        if (Owner.Player is { } player && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            foreach (var listener in player.Relics.OfType<IGremlinWizConsumedListener>().ToList())
            {
                if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    break;
                await listener.OnWizConsumedAsync(choiceContext);
            }
        }
    }
}
