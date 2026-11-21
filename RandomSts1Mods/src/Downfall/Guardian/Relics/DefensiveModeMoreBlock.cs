using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Guardian.Relics;

public sealed class DefensiveModeMoreBlock : GuardianRelic, IBraceBonusProvider
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public int ExtraBraceAmount => 1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.BraceHoverTip
    ];

    public void OnBraceBonusApplied() => Flash();
}
