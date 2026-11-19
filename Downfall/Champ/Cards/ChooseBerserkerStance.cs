using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Champ.Cards;

public sealed class ChooseBerserkerStance()
    : ChampCard(-1, CardType.Skill, CardRarity.Token, TargetType.None, showInCardLibrary: false)
{
    public override int MaxUpgradeLevel => 0;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BerserkerHoverTip
    ];
}
