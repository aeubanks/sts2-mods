using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class FreezeAndBurnPower : PackmasterPower
{
    protected override string IconKey => "freeze_and_burn_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || Owner.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
            return;

        int damage = Amount;
        Flash();
        await PowerCmd.Remove(this);

        if (damage > 0)
        {
            await CreatureCmd.Damage(
                choiceContext,
                combatState.HittableEnemies,
                damage,
                ValueProp.Move,
                dealer: Owner,
                cardSource: null,
                cardPlay: null);
        }
    }
}
