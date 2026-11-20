using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Guardian.Powers;

public sealed class AutomayhemPower : GuardianPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.AccelerateHoverTip,
        GuardianCard.StasisHoverTip
    ];

    public override async Task AfterAutoPrePlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0
            || player.Creature != Owner
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || GuardianMechanics.GetStasisOrbs(player).Count == 0)
        {
            return;
        }

        Flash();
        await GuardianMechanics.AccelerateAsync(choiceContext, player, Amount);
    }
}
