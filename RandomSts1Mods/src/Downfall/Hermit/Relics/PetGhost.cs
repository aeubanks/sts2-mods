using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Relics;

public sealed class PetGhost : HermitRelic
{
    private bool _usedThisCombat;

    protected override string IconKey => "pet_ghost";

    public override RelicRarity Rarity => RelicRarity.Shop;

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

    public override Task AfterModifyingHpLostAfterOsty()
    {
        if (!_usedThisCombat)
        {
            _usedThisCombat = true;
            Status = RelicStatus.Disabled;
            Flash();
        }
        return Task.CompletedTask;
    }
}
