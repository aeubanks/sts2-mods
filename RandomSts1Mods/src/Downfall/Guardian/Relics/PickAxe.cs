using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Guardian.Relics;

public sealed class PickAxe : GuardianRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.GemHoverTip
    ];

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (room.RoomType == RoomType.Elite)
        {
            Flash();
            room.AddExtraReward(Owner, GuardianGems.CreateGemCardReward(Owner, choiceCount: 1));
        }

        return Task.CompletedTask;
    }
}
