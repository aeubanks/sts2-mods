using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Collector.Relics;

/// <summary>
/// Jade Ring (Uncommon):
/// Enemies take 6 more damage from Doom.
/// </summary>
public sealed class JadeRing : CollectorRelic, IDoomBonusProvider
{
    public const int DoomBonus = 6;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.DoomHoverTip
    ];

    public int GetDoomDamageBonus() => DoomBonus;
}
