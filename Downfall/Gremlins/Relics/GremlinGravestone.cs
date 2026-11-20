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
        new HealVar(5m)
    ];

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not RestSiteRoom || !Owner.Creature.IsAlive)
            return;

        Flash();
        if (Owner.Character is TheGremlins && GremlinMob.CanResurrect(Owner))
        {
            GremlinMob.ResurrectRandom(Owner, Owner.Creature.MaxHp, preferCombatRng: false);
            return;
        }

        await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        if (Owner.Character is TheGremlins)
        {
            GremlinMob.SyncFrontHpFromCreature(Owner);
            GremlinMob.HealStandbyGremlins(Owner, DynamicVars.Heal.IntValue);
        }
    }
}
