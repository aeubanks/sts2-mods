using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Hermit.Powers;

public sealed class DeterminationPower : HermitPower
{
    protected override string IconKey => "determination_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power.Owner != Owner
            || ReferenceEquals(power, this)
            || !Owner.IsAlive
            || Amount <= 0
            || amount == 0m
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        bool isDebuffApplied =
            (power.Type == PowerType.Debuff && amount > 0m)
            || (power.Type == PowerType.Buff && power.AllowNegative && amount < 0m);
        if (isDebuffApplied)
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount, Owner, null);
        }
    }
}
