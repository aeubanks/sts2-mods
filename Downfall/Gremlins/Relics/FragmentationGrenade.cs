using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Relics;

public sealed class FragmentationGrenade : GremlinRelic, IRandomDamageBonusProvider
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public decimal BonusRandomEnemyDamage => DynamicVars.Damage.BaseValue;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Unpowered)
    ];

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (dealer != Owner.Creature
            || !props.IsPoweredAttack()
            || cardSource?.TargetType != TargetType.RandomEnemy)
        {
            return 0m;
        }

        return DynamicVars.Damage.BaseValue;
    }
}
