using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Slimebound.Powers;

/// <summary>
/// Temporary Thorns lasting until the end of the opposing side's turn.
/// Used by Slime Spikes and Protector (Spiky) Slime.
/// </summary>
public sealed class SlimeTempThornsPower : SlimePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ThornsPower>()
    ];

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(power, this) && amount > 0m)
        {
            await PowerCmd.Apply<ThornsPower>(choiceContext, Owner, amount, applier, cardSource, silent: true);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Side || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int toRemove = Amount;
        Flash();
        await PowerCmd.Remove(this);
        var thorns = Owner.GetPower<ThornsPower>();
        if (thorns is not null && toRemove > 0)
        {
            int remove = Math.Min(thorns.Amount, toRemove);
            if (remove > 0)
            {
                await PowerCmd.ModifyAmount(choiceContext, thorns, -remove, Owner, null);
            }
        }
    }
}
