using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Gremlins.Relics;

public sealed class GremlinGravestone : GremlinRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(15m)
    ];

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not RestSiteRoom || !Owner.Creature.IsAlive)
            return;

        Flash();
        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
    }
}
