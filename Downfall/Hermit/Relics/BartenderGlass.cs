using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Hermit.Relics;

public sealed class BartenderGlass : HermitRelic
{
    private const int UsesPerCombat = 2;
    private int _remaining;
    private bool _inCombat;

    protected override string IconKey => "bartender_glass";

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool ShowCounter => _inCombat;
    public override int DisplayAmount => _remaining;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("PotionTriggers", UsesPerCombat)
    ];

    public override Task AfterObtained()
    {
        if (Owner.Creature.CombatState is not null)
        {
            _inCombat = true;
            _remaining = UsesPerCombat;
            Status = RelicStatus.Normal;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        _inCombat = true;
        _remaining = UsesPerCombat;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _inCombat = false;
        _remaining = 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        if (potion.Owner != Owner || Owner.Creature.CombatState is null || _remaining <= 0)
            return;

        _remaining--;
        if (_remaining <= 0)
            Status = RelicStatus.Disabled;
        InvokeDisplayAmountChanged();
        Flash();

        var randomPotion = PotionFactory.CreateRandomPotionInCombat(
            Owner,
            Owner.RunState.Rng.CombatPotionGeneration).ToMutable();
        await PotionCmd.TryToProcure(randomPotion, Owner);
    }
}
