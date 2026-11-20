using BaseLib.Utils;
using Downfall.Collector.Powers;
using Downfall.Core;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Collector;

[Pool(typeof(CollectorCardPool))]
public abstract class CollectorCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    protected override string CharacterKey => TheCollector.CharKey;

    /// <summary>
    /// True when playing this card requires Exhausting another card from Hand (Pyre).
    /// </summary>
    public virtual bool IsPyre => false;

    public static IHoverTip DoomHoverTip => HoverTipFactory.FromPower<CollectorDoomPower>();
    public static IHoverTip ReservesHoverTip => HoverTipFactory.FromPower<ReservesPower>();
    public static IHoverTip TempHpHoverTip => HoverTipFactory.FromPower<DownfallTempHpPower>();

    public static IHoverTip PyreHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_PYRE.title"),
        new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_PYRE.description"));

    public static IHoverTip AfflictedHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_AFFLICTED.title"),
        new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_AFFLICTED.description"));

    public static IHoverTip CollectedHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_COLLECTED.title"),
        new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_COLLECTED.description"));

    public static IHoverTip TorchHeadHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_TORCHHEAD.title"),
        new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_TORCHHEAD.description"));

    protected override bool IsPlayable
    {
        get
        {
            if (IsPyre && IsMutable && Owner?.PlayerCombatState is { } pcs)
            {
                return pcs.Hand.Cards.Any(c => !ReferenceEquals(c, this));
            }
            return base.IsPlayable;
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        CardModel? pyredCard = null;
        if (IsPyre && Owner.PlayerCombatState is { } pcs)
        {
            var otherHandCards = pcs.Hand.Cards.Where(c => !ReferenceEquals(c, this)).ToList();
            if (otherHandCards.Count > 0)
            {
                if (otherHandCards.Count == 1)
                {
                    pyredCard = otherHandCards[0];
                }
                else
                {
                    var prompt = new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_PYRE.selectPrompt");
                    var prefs = new CardSelectorPrefs(prompt, 1);
                    pyredCard = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, c => !ReferenceEquals(c, this), this))
                        .FirstOrDefault();
                }

                if (pyredCard is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                {
                    // Exhaust pyred card FIRST so a full 10-card hand has room for ShootingStarPower's free copy.
                    await CardCmd.Exhaust(choiceContext, pyredCard);

                    if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                    {
                        foreach (var listener in Owner.Creature.Powers.OfType<IOnPyreListener>().ToList())
                        {
                            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                                break;
                            await listener.OnPyreAsync(choiceContext, Owner, pyredCard);
                        }
                    }
                }
            }
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await OnCollectorPlay(choiceContext, cardPlay, pyredCard);
        }
    }

    protected virtual Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard) =>
        Task.CompletedTask;

    internal static Creature? ResolveTarget(MegaCrit.Sts2.Core.Entities.Players.Player owner, Creature? target)
    {
        if (target is { IsAlive: true })
            return target;

        if (owner.Creature.CombatState is not { } combatState)
            return null;

        return owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies.Where(e => e.IsAlive).ToList());
    }
}

[Pool(typeof(TokenCardPool))]
public abstract class CollectorTokenCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = false)
    : CollectorCard(cost, type, rarity, target, showInCardLibrary);

/// <summary>
/// Base class for all Collector Collectible cards (monster souls stored in the Collection pile).
/// Inheriting CollectibleCard automatically gives CardKeyword.Exhaust and preserves Collected identity
/// across CreateClone(), pile moves, Soulforge, Forgery, Omen, DoubleTrouble, ShadowDaggers, and save/load.
/// </summary>
public abstract class CollectibleCard(
    int cost,
    CardType type,
    TargetType target)
    : CollectorTokenCard(cost, type, CardRarity.Token, target, showInCardLibrary: false)
{
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        Type == CardType.Power ? [] : [CardKeyword.Exhaust];
}
