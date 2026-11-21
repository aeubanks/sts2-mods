using Downfall.Core;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Relics;

/// <summary>
/// Red Candle (CandleOfCauterizing): Whenever you apply Soulburn, apply 2 additional Soulburn.
/// </summary>
public sealed class CandleOfCauterizing : HexaRelic, ISoulburnBonusProvider
{
    public const int BonusAmount = 2;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SoulburnPower>(BonusAmount)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.SoulburnHoverTip
    ];

    public int GetSoulburnBonus(Player player) => player == Owner ? BonusAmount : 0;

    public void NotifySoulburnBonusTriggered(Player player)
    {
        if (player == Owner)
            Flash();
    }
}
