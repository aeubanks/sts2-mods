using BaseLib.Utils;
using Downfall.Core;
using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace Downfall.Slimebound;

[Pool(typeof(SlimeboundCardPool))]
public abstract class SlimeCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true) : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    protected override string CharacterKey => TheSlimeBoss.CharKey;

    public virtual bool IsTackle => false;
    public virtual bool IsLick => false;

    internal static IHoverTip SplitHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-SPLIT_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-SPLIT_KEYWORD.description"));

    internal static IHoverTip CommandHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-COMMAND_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-COMMAND_KEYWORD.description"));

    internal static IHoverTip AbsorbHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-ABSORB_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-ABSORB_KEYWORD.description"));

    internal static IHoverTip TackleHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-TACKLE_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-TACKLE_KEYWORD.description"));

    internal static IHoverTip ConsumeHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-CONSUME_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-CONSUME_KEYWORD.description"));

    internal static IHoverTip GoopHoverTip => HoverTipFactory.FromPower<SlimedPower>();
    internal static IHoverTip PotencyHoverTip => HoverTipFactory.FromPower<PotencyPower>();
}
