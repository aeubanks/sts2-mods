using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Powers;

/// <summary>
/// Feel My Pain (FeelMyPainPower): Whenever you Exhaust a card, a random enemy loses Amount HP.
/// </summary>
public sealed class FeelMyPainPower : CollectorPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var target = CollectorCard.ResolveTarget(Owner.Player!, null);
        if (target is null)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            target,
            Amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner);
    }
}
