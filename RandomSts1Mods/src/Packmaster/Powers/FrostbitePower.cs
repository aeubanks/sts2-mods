using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class FrostbitePower : PackmasterPower
{
    protected override string IconKey => "frostbite_power";

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(Owner) && Owner.IsAlive && Amount > 0)
        {
            await TriggerDamageAsync(new ThrowingPlayerChoiceContext(), reduce: true);
        }
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner && Owner.IsAlive && dealer is not null && dealer != Owner && props.IsPoweredAttack())
        {
            await PowerCmd.Apply<FrostbitePower>(choiceContext, Owner, 1m, dealer, cardSource);
        }
    }

    public async Task TriggerDamageAsync(PlayerChoiceContext choiceContext, bool reduce)
    {
        if (!Owner.IsAlive || Amount <= 0)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            Amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: null,
            cardSource: null,
            cardPlay: null);

        if (!Owner.IsAlive || !reduce)
            return;

        var snowedIn = Owner.GetPower<SnowedInPower>();
        if (snowedIn is not null)
        {
            snowedIn.TriggerFlash();
            return;
        }

        int reduction = (Amount + 1) / 2;
        await PowerCmd.ModifyAmount(choiceContext, this, -reduction, null, null);
    }
}
