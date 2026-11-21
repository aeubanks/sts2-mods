using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Gremlins.Powers;

public sealed class HecklePower : GremlinPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GremlinCard.TempHpHoverTip
    ];

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (applier != Owner
            || power.Owner == Owner
            || power.Owner.Side == Owner.Side
            || power is TemporaryStrengthPower
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        bool isDebuffApplied = (power.Type == PowerType.Debuff && amount > 0m)
            || (power.Type == PowerType.Buff && power.AllowNegative && amount < 0m);
        if (!isDebuffApplied)
            return;

        Flash();
        await PowerCmd.Apply<DownfallTempHpPower>(choiceContext, Owner, Amount, Owner, null);
    }
}
