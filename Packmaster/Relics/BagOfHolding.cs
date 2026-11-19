using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Packmaster.Relics;

public sealed class BagOfHolding : PackmasterRelic
{
    private int _turnsRemaining;

    protected override string IconKey => "bag_of_holding";

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool HasUponPickupEffect => true;
    public override bool ShowCounter => _turnsRemaining > 0;
    public override int DisplayAmount => _turnsRemaining;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override bool IsAllowedInternal(IRunState runState) =>
        runState.Players.Any(p => p.Relics.Any(r => r is HandyHaversack));

    public override async Task AfterObtained()
    {
        if (Owner.Relics.FirstOrDefault(r => r is HandyHaversack) is { } haversack)
        {
            await RelicCmd.Remove(haversack);
        }
    }

    public override Task BeforeCombatStart()
    {
        _turnsRemaining = Owner.Deck.Cards.Count / 5;
        Status = _turnsRemaining > 0 ? RelicStatus.Normal : RelicStatus.Disabled;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner || _turnsRemaining <= 0 || !Owner.Creature.IsAlive)
            return;

        _turnsRemaining--;
        Flash();
        if (_turnsRemaining <= 0)
        {
            Status = RelicStatus.Disabled;
        }
        InvokeDisplayAmountChanged();
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _turnsRemaining = 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
