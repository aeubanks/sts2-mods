using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Hermit.Relics;

public sealed class BloodyTooth : HermitRelic
{
    protected override string IconKey => "bloody_tooth";

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(7m),
        new GoldVar(35)
    ];

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (room.RoomType != RoomType.Elite || !Owner.Creature.IsAlive)
            return;

        Flash();
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        await PlayerCmd.GainGold(DynamicVars.Gold.IntValue, Owner);
    }
}
