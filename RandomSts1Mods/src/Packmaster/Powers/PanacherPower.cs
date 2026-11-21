using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class PanacherPower : PackmasterPower
{
    private const decimal TriggerEvery = 5m;

    protected override string IconKey => "panacher_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Countdown", TriggerEvery)
    ];

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (Owner.Player is null
            || card.Owner != Owner.Player
            || !Owner.IsAlive
            || Owner.CombatState is null
            || CombatManager.Instance.IsOverOrEnding)
            return;

        DynamicVars["Countdown"].BaseValue -= 1m;
        if (DynamicVars["Countdown"].IntValue > 0)
            return;

        DynamicVars["Countdown"].BaseValue = TriggerEvery;
        Flash();
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner.CombatState.HittableEnemies,
            Amount,
            ValueProp.Unpowered,
            dealer: Owner,
            cardSource: null,
            cardPlay: null);
    }
}
