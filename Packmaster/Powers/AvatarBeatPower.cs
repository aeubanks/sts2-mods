using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class AvatarBeatPower : PackmasterPower
{
    private bool _initialized;

    protected override string IconKey => "avatar_beat_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Threshold", 10m),
        new IntVar("Draws", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this && amount > 0)
        {
            if (_initialized)
            {
                DynamicVars["Draws"].BaseValue += 1m;
            }
            else
            {
                _initialized = true;
            }
        }
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (dealer != Owner
            || target == Owner
            || !Owner.IsAlive
            || !props.IsPoweredAttack()
            || result.UnblockedDamage < DynamicVars["Threshold"].IntValue)
        {
            return;
        }

        Flash();
        DynamicVars["Threshold"].BaseValue += 5m;
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, Amount, Owner, null);
        if (Owner.Player is not null && Owner.IsAlive)
        {
            await CardPileCmd.Draw(choiceContext, DynamicVars["Draws"].BaseValue, Owner.Player);
        }
    }
}
