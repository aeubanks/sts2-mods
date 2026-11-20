using BaseLib.Utils;
using Downfall.Core;
using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost;

[Pool(typeof(HexaCardPool))]
public abstract class HexaCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    protected override string CharacterKey => TheHexaghost.CharKey;

    public static IHoverTip AfterlifeHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-HEXA_AFTERLIFE.title"),
        new LocString("gameplay_ui", "DOWNFALL-HEXA_AFTERLIFE.description"));

    public static IHoverTip GhostflameHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-HEXA_GHOSTFLAME.title"),
        new LocString("gameplay_ui", "DOWNFALL-HEXA_GHOSTFLAME.description"));

    public static IHoverTip IgniteHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-HEXA_IGNITE.title"),
        new LocString("gameplay_ui", "DOWNFALL-HEXA_IGNITE.description"));

    public static IHoverTip AdvanceHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-HEXA_ADVANCE.title"),
        new LocString("gameplay_ui", "DOWNFALL-HEXA_ADVANCE.description"));

    public static IHoverTip RetractHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-HEXA_RETRACT.title"),
        new LocString("gameplay_ui", "DOWNFALL-HEXA_RETRACT.description"));

    public static IHoverTip ExtinguishHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-HEXA_EXTINGUISH.title"),
        new LocString("gameplay_ui", "DOWNFALL-HEXA_EXTINGUISH.description"));

    public static IHoverTip SoulburnHoverTip => HoverTipFactory.FromPower<SoulburnPower>();
    public static IHoverTip IntensityHoverTip => HoverTipFactory.FromPower<EnhancePower>();

    /// <summary>
    /// Whether this card triggers an Afterlife effect when Exhausted.
    /// </summary>
    public virtual bool HasAfterlife => false;

    /// <summary>
    /// Override on Afterlife cards to perform their purple exhaust effect.
    /// Runs whenever this card is Exhausted (whether by Ethereal or any other Exhaust).
    /// </summary>
    protected virtual Task OnAfterlifeAsync(PlayerChoiceContext choiceContext, bool causedByEthereal) =>
        Task.CompletedTask;

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == CombatSide.Player
            && combatState.RoundNumber == 1
            && Owner is not null
            && Owner.Character is TheHexaghost
            && participants.Contains(Owner.Creature)
            && !HexaCombatState.Get(combatState).WheelInitialized)
        {
            await HexaMechanics.EnsureWheelInitializedAsync(choiceContext, Owner);
        }
    }

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card != this
            || !HasAfterlife
            || Owner is null
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await OnAfterlifeAsync(choiceContext, causedByEthereal);
    }

    protected Task ApplySoulburnAsync(PlayerChoiceContext choiceContext, Creature target, int baseAmount)
    {
        return HexaMechanics.ApplySoulburnAsync(choiceContext, target, baseAmount, Owner.Creature, this);
    }
}
