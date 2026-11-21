using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Guardian.Powers;

public sealed class SpikerProtocolPower : GuardianPower, IModeShiftTriggerListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.DefensiveModeHoverTip,
        HoverTipFactory.FromPower<ThornsPower>()
    ];

    public async Task OnModeShiftTriggeredAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0
            || player.Creature != Owner
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<ThornsPower>(choiceContext, Owner, Amount, Owner, null);
    }
}
