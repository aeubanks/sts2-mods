using MegaCrit.Sts2.Core.Entities.Relics;

namespace Downfall.Collector.Relics;

/// <summary>
/// The Contract (Shop):
/// Upon pickup, gain 10 Essence.
/// </summary>
public sealed class TheContract : CollectorRelic
{
    public const int EssenceGain = 10;

    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool HasUponPickupEffect => true;

    public override Task AfterObtained()
    {
        if (Owner is not null)
        {
            CollectorRunState.GetOrCreate(Owner).ChangeEssence(Owner, EssenceGain);
        }
        return Task.CompletedTask;
    }
}
