using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Relics;

/// <summary>
/// The Bagpipes (Bagpipes, Rare):
/// Whenever you apply Weak or Vulnerable to an enemy, increase the amount applied by 1.
/// </summary>
public sealed class Bagpipes : CollectorRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    public override decimal ModifyPowerAmountGivenAdditive(
        PowerModel power,
        Creature giver,
        decimal amount,
        Creature? target,
        CardModel? cardSource)
    {
        if (Owner is not null
            && giver == Owner.Creature
            && target is { IsEnemy: true }
            && amount > 0m
            && power is WeakPower or VulnerablePower)
        {
            Flash();
            return 1m;
        }
        return 0m;
    }
}
