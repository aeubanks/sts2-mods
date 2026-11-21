using BaseLib.Utils;
using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Snecko;

[Pool(typeof(SneckoCardPool))]
public abstract class SneckoCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    private bool _overflowCaptured;
    private bool _overflowWhenPlayed;
    private bool _giftClaimed;

    protected override string CharacterKey => TheSnecko.CharKey;

    public virtual bool UsesOverflow => false;
    public virtual bool IsGift => false;

    internal bool IsTyphoonAutoPlay { get; set; }

    [SavedProperty]
    public bool GiftClaimed
    {
        get => _giftClaimed;
        set
        {
            AssertMutable();
            _giftClaimed = value;
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _overflowCaptured = false;
        _overflowWhenPlayed = false;
        IsTyphoonAutoPlay = false;
    }

    protected bool IsOverflowInHand()
    {
        if (!UsesOverflow || IsTyphoonAutoPlay || CombatState is null || Pile?.Type != PileType.Hand)
            return false;

        return SneckoMechanics.EvaluateOverflow(Owner);
    }

    protected internal bool IsOverflowActiveNow
    {
        get
        {
            if (!UsesOverflow || IsTyphoonAutoPlay || CombatState is null)
                return false;

            if (Pile?.Type == PileType.Play && _overflowCaptured)
                return _overflowWhenPlayed;

            return SneckoMechanics.EvaluateOverflow(Owner);
        }
    }

    protected async Task<bool> TryTriggerOverflowAsync(PlayerChoiceContext choiceContext)
    {
        if (!IsOverflowActiveNow
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return false;
        }

        await SneckoMechanics.NotifyOverflowTriggeredAsync(choiceContext, Owner, this);
        return true;
    }

    protected override bool ShouldGlowGoldInternal => UsesOverflow && IsOverflowInHand();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        BuildDefaultHoverTips();

    private IEnumerable<IHoverTip> BuildDefaultHoverTips()
    {
        if (UsesOverflow)
            yield return OverflowHoverTip;
        if (IsGift)
            yield return GiftHoverTip;
    }

    internal static IHoverTip MuddleHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-MUDDLE_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-MUDDLE_KEYWORD.description"));

    internal static IHoverTip OverflowHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-OVERFLOW_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-OVERFLOW_KEYWORD.description"));

    internal static IHoverTip OffclassHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-OFFCLASS_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-OFFCLASS_KEYWORD.description"));

    internal static IHoverTip GiftHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-GIFT_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-GIFT_KEYWORD.description"));

    internal static IHoverTip LuckyHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-LUCKY_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-LUCKY_KEYWORD.description"));

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (ReferenceEquals(card, this))
        {
            if (UsesOverflow && Pile?.Type == PileType.Play && CombatState is not null)
            {
                int countOffset = oldPileType == PileType.Hand ? 1 : 0;
                _overflowWhenPlayed = !IsTyphoonAutoPlay && SneckoMechanics.EvaluateOverflow(Owner, countOffset);
                _overflowCaptured = true;
            }
            else if (Pile?.Type != PileType.Play)
            {
                _overflowCaptured = false;
                _overflowWhenPlayed = false;
            }
        }

        if (IsGift
            && ReferenceEquals(card, this)
            && oldPileType == PileType.None
            && Pile?.Type == PileType.Deck
            && RunManager.Instance.IsInProgress
            && !GiftClaimed)
        {
            GiftClaimed = true;
            await OnGiftClaimedAsync();
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && cardPlay.PlayIndex >= cardPlay.PlayCount - 1)
        {
            _overflowCaptured = false;
            _overflowWhenPlayed = false;
            IsTyphoonAutoPlay = false;
        }
        return Task.CompletedTask;
    }

    protected virtual Task OnGiftClaimedAsync() => Task.CompletedTask;
}
