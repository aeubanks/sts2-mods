using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Powers;

public sealed class FatGremlinPower : GremlinActivePower
{
    private HashSet<Creature>? _weakenedThisPlay;

    public override GremlinKind Kind => GremlinKind.Fat;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _weakenedThisPlay = null;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player)
        {
            _weakenedThisPlay?.Clear();
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player)
        {
            _weakenedThisPlay?.Clear();
        }
        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer != Owner
            || target == Owner
            || !props.IsPoweredAttack()
            || cardSource is not { Type: CardType.Attack }
            || Amount <= 0
            || !target.IsAlive
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _weakenedThisPlay ??= [];
        if (!_weakenedThisPlay.Add(target))
            return;

        Flash();
        await PowerCmd.Apply<WeakPower>(choiceContext, target, Amount, Owner, null);
    }
}
