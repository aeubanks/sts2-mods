using BaseLib.Utils;
using Downfall.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace Downfall.Champ;

[Pool(typeof(ChampCardPool))]
public abstract class ChampCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    protected override string CharacterKey => TheChamp.CharKey;

    public virtual bool IsFinisher => false;
    public virtual bool IsCombo => false;

    protected override bool IsPlayable =>
        !IsFinisher
        || !IsMutable
        || Owner is null
        || ChampStances.IsInStance(Owner.Creature);

    protected override bool ShouldGlowGoldInternal =>
        IsFinisher && CombatState is not null && IsPlayable;

    internal static IHoverTip BerserkerHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-BERSERKER_STANCE_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-BERSERKER_STANCE_KEYWORD.description"));

    internal static IHoverTip DefensiveHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-DEFENSIVE_STANCE_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-DEFENSIVE_STANCE_KEYWORD.description"));

    internal static IHoverTip UltimateHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-ULTIMATE_STANCE_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-ULTIMATE_STANCE_KEYWORD.description"));

    internal static IHoverTip FinisherHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-FINISHER_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-FINISHER_KEYWORD.description"));

    internal static IHoverTip ComboHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-COMBO_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-COMBO_KEYWORD.description"));
}
