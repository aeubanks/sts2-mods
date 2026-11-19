using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Relics;

public sealed class ShortStature : GremlinRelic
{
    private bool _usedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("HealPercent", 20m)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _usedThisCombat = false;
    }

    public override Task BeforeCombatStart()
    {
        _usedThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _usedThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (CombatManager.Instance.IsInProgress
            && target == Owner.Creature
            && !_usedThisCombat
            && target.CurrentHp > 0
            && amount >= target.CurrentHp)
        {
            return 0m;
        }

        return amount;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        if (_usedThisCombat)
            return;

        _usedThisCombat = true;
        Status = RelicStatus.Disabled;
        Flash();

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            int healAmt = Math.Max(1, (int)(Owner.Creature.MaxHp * DynamicVars["HealPercent"].BaseValue / 100m));
            await CreatureCmd.Heal(Owner.Creature, healAmt);
        }
    }
}
