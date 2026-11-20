using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace Downfall.Guardian.Powers;

public sealed class GuardianStunnedPower : GuardianPower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override bool ShouldDraw(Player player, bool fromHandDraw) =>
        !(fromHandDraw && Amount > 0 && player.Creature == Owner);

    public override async Task AfterAutoPrePlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0
            || player.Creature != Owner
            || !Owner.IsAlive
            || !Owner.Powers.Contains(this)
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Decrement(this);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            PlayerCmd.EndTurn(player, canBackOut: false);
        }
    }
}
