using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Relics;

/// <summary>
/// Olexa's Shield (JarOfFuel): Powers are counted towards the progression
/// of Searing and Crushing Ghostflames.
/// </summary>
public sealed class JarOfFuel : HexaRelic, IGhostflamePowerTriggerProvider
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.GhostflameHoverTip
    ];

    public bool AllowsPowerTrigger(Player player) => player == Owner;

    public void NotifyPowerTrigger(Player player)
    {
        if (player == Owner)
            Flash();
    }
}
