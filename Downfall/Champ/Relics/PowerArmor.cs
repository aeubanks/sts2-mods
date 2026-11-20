using Downfall.Champ.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Champ.Relics;

public sealed class PowerArmor : ChampRelic
{
    public const int Cap = 10;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new DynamicVar("Cap", Cap)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        HoverTipFactory.FromPower<VigorPower>(),
        HoverTipFactory.FromPower<CounterPower>()
    ];

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner)
            return amount;

        return amount + DynamicVars.Energy.BaseValue;
    }

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        if (target == Owner.Creature && amount > 0m)
        {
            int current;
            if (canonicalPower is VigorPower)
            {
                current = target.GetPowerAmount<VigorPower>();
            }
            else if (canonicalPower is CounterPower)
            {
                current = target.GetPowerAmount<CounterPower>();
            }
            else
            {
                modifiedAmount = amount;
                return false;
            }

            int allowed = Math.Max(0, Cap - current);
            if (amount > allowed)
            {
                Flash();
                modifiedAmount = allowed;
                return true;
            }
        }

        modifiedAmount = amount;
        return false;
    }
}
