using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Guardian.Powers;

public sealed class GemFinderPower : GuardianPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.GemHoverTip
    ];

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (Amount <= 0 || Owner.Player is not { } player)
            return Task.CompletedTask;

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            room.AddExtraReward(player, GuardianGems.CreateGemCardReward(player, choiceCount: 1));
        }

        return Task.CompletedTask;
    }
}
