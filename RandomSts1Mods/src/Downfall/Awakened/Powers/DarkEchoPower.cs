using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Awakened.Powers;

public sealed class DarkEchoPower : AwakenedPower
{
    public const decimal BaseDamage = 4m;

    protected override string IconKey => "dark_echo_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(BaseDamage, ValueProp.Unpowered)
    ];

    private decimal ComputeDamage() =>
        Math.Max(0m, BaseDamage + Owner.GetPowerAmount<StrengthPower>());

    private void SyncDisplayedDamage()
    {
        DynamicVars.Damage.BaseValue = ComputeDamage();
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SyncDisplayedDamage();
        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power.Owner == Owner && power is StrengthPower)
        {
            SyncDisplayedDamage();
        }
        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side
            || !participants.Contains(Owner)
            || !Owner.IsAlive
            || CombatState is null
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        decimal damage = ComputeDamage();
        SyncDisplayedDamage();
        if (damage <= 0m || Amount <= 0)
            return;

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding || CombatState.HittableEnemies.Count == 0)
                break;

            foreach (var enemy in CombatState.HittableEnemies.ToList())
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;
                if (!enemy.IsAlive)
                    continue;

                await CreatureCmd.Damage(
                    choiceContext,
                    enemy,
                    damage,
                    ValueProp.Unpowered,
                    dealer: Owner,
                    cardSource: null,
                    cardPlay: null);
            }
        }
    }
}
