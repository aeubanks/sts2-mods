using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Hermit.Relics;

public sealed class Horseshoe : HermitRelic
{
    protected override string IconKey => "horseshoe";

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<FrailPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        if (target == Owner.Creature
            && amount > 0m
            && canonicalPower is WeakPower or FrailPower or VulnerablePower)
        {
            modifiedAmount = Math.Max(0m, amount - 1m);
            return true;
        }

        modifiedAmount = amount;
        return false;
    }

    public override Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        if (power.Owner == Owner.Creature)
        {
            Flash();
        }
        return Task.CompletedTask;
    }
}
