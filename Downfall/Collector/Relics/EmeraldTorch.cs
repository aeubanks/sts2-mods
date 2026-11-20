using Downfall.Collector.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Collector.Relics;

/// <summary>
/// Shared base for Collector's starter torch relics (EmeraldTorch & PrismaticTorch).
/// Persists Essence, Collection CSV, and Bottled Collection Index via [SavedProperty],
/// handles Collection combat init & start-of-turn draws, post-combat Essence + CollectibleCardReward,
/// Rest Site Release option, and Billow/Bellow expired-Block tracking.
/// </summary>
public abstract class CollectorTorchRelic : CollectorRelic
{
    private int _savedEssence = CollectorRunState.StartingEssence;
    private string _savedCollectionCsv = string.Empty;
    private int _savedBottledIndex = -1;
    private bool _firstTurnTriggered;

    [SavedProperty]
    public int SavedEssence
    {
        get => _savedEssence;
        set
        {
            AssertMutable();
            _savedEssence = value;
            InvokeDisplayAmountChanged();
        }
    }

    [SavedProperty]
    public string SavedCollectionCsv
    {
        get => _savedCollectionCsv;
        set
        {
            AssertMutable();
            _savedCollectionCsv = value ?? string.Empty;
        }
    }

    [SavedProperty]
    public int SavedBottledIndex
    {
        get => _savedBottledIndex;
        set
        {
            AssertMutable();
            _savedBottledIndex = value;
        }
    }

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool ShowCounter => IsMutable && Owner is not null;
    public override int DisplayAmount => _savedEssence;

    internal void SyncFromRunState(int essence, string collectionCsv, int bottledIndex)
    {
        AssertMutable();
        _savedEssence = essence;
        _savedCollectionCsv = collectionCsv;
        _savedBottledIndex = bottledIndex;
        InvokeDisplayAmountChanged();
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _firstTurnTriggered = false;
    }

    public override Task AfterRemoved()
    {
        // Hydrate directly from this torch instance in case the run was loaded right at Touch of Orobas
        // and EmeraldTorch was already detached from player.Relics before AfterRemoved runs.
        if (Owner is not null)
        {
            CollectorRunState.EnsureHydratedFromTorch(Owner, this);
        }
        return Task.CompletedTask;
    }

    public override Task AfterObtained()
    {
        if (Owner is not null)
        {
            // When Touch of Orobas replaces EmeraldTorch -> PrismaticTorch, copy existing CollectorRunState into this relic.
            var existing = CollectorRunState.TryGetReadOnly(Owner);
            if (existing is not null)
            {
                existing.SyncToTorch(Owner);
            }
            else
            {
                _ = CollectorRunState.GetOrCreate(Owner);
            }
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        _firstTurnTriggered = false;
        if (Owner?.Creature.CombatState is { } combatState)
        {
            CollectorMechanics.InitializeCombatCollection(combatState, Owner);
        }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _firstTurnTriggered = false;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (Owner is not null && side == Owner.Creature.Side)
        {
            var data = CollectorCombatState.Get(combatState).GetPlayer(Owner);
            data.LastTurnUnusedBlock = 0;
            data.BlockBeforeTurnStartClear = Owner.Creature.Block;
        }
        return Task.CompletedTask;
    }

    public override Task AfterBlockCleared(Creature creature)
    {
        if (Owner is not null && creature == Owner.Creature && creature.CombatState is { } combatState)
        {
            var data = CollectorCombatState.Get(combatState).GetPlayer(Owner);
            data.LastTurnUnusedBlock = Math.Max(0, data.BlockBeforeTurnStartClear - creature.Block);
        }
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (Owner is not null && side == Owner.Creature.Side)
        {
            var data = CollectorCombatState.Get(combatState).GetPlayer(Owner);
            data.BlockBeforeTurnStartClear = 0;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (!_firstTurnTriggered)
        {
            _firstTurnTriggered = true;
            await OnFirstTurnStartAsync(choiceContext, player);
        }

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        // Draw Collected card(s) at the start of each turn (after normal 5-card draw).
        int draws = CollectorMechanics.GetTurnStartCollectionDrawCount(player);
        for (int i = 0; i < draws; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await CollectorMechanics.DrawFromCollectionAsync(choiceContext, player);
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner
            && cardPlay.Card is CollectibleCard
            && Owner.Creature.CombatState is { } combatState)
        {
            CollectorCombatState.Get(combatState).GetPlayer(Owner).CollectedCardsPlayedThisCombat++;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCombatVictory(CombatRoom room)
    {
        if (Owner is null)
            return Task.CompletedTask;

        var state = CollectorRunState.GetOrCreate(Owner);
        int essenceGain = CollectorMechanics.GetEssenceRewardForRoom(room.RoomType);
        state.ChangeEssence(Owner, essenceGain);

        var collectible = CollectorMechanics.CreateCollectibleForRoom(Owner, room);
        room.AddExtraReward(Owner, new CollectibleCardReward(collectible, Owner));
        return Task.CompletedTask;
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner)
            return false;

        if (!options.Any(o => o is StashAwayRestSiteOption))
            options.Add(new StashAwayRestSiteOption(player));
        return true;
    }

    protected abstract Task OnFirstTurnStartAsync(PlayerChoiceContext choiceContext, Player player);

    protected IHoverTip BuildEssenceCollectionHoverTip()
    {
        var title = new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_COLLECTION_STATUS.title");
        var desc = new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_COLLECTION_STATUS.description");
        var ro = CollectorRunState.TryGetReadOnly(IsMutable ? Owner : null);
        int essence = ro?.Essence ?? _savedEssence;
        int count = ro?.Collection.Count ?? CountSavedCollectionEntries(_savedCollectionCsv);
        desc.Add("Essence", essence);
        desc.Add("Count", count);
        return new HoverTip(title, desc);
    }

    private static int CountSavedCollectionEntries(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return 0;

        int count = 0;
        foreach (var part in csv.Split(';'))
        {
            if (!string.IsNullOrWhiteSpace(part))
                count++;
        }
        return count;
    }
}

/// <summary>
/// Starter relic: Emerald Torch — Start each combat with 1 Reserve.
/// Upgrades via Touch of Orobas into Prismatic Torch.
/// </summary>
public sealed class EmeraldTorch : CollectorTorchRelic
{
    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<PrismaticTorch>();

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.ReservesHoverTip,
        BuildEssenceCollectionHoverTip()
    ];

    protected override async Task OnFirstTurnStartAsync(PlayerChoiceContext choiceContext, Player player)
    {
        Flash();
        await CollectorMechanics.GainReservesAsync(choiceContext, player, 1);
    }
}

/// <summary>
/// Orobas upgrade of Emerald Torch: Prismatic Torch —
/// At the start of each combat, add an Ember into your Hand and gain 1 Reserve.
/// The first time you Exhaust an Ember each turn, gain 1 Reserve.
/// </summary>
public sealed class PrismaticTorch : CollectorTorchRelic
{
    private bool _triggeredEmberThisTurn;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.ReservesHoverTip,
        HoverTipFactory.FromCard<Ember>(),
        BuildEssenceCollectionHoverTip()
    ];

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _triggeredEmberThisTurn = false;
    }

    public override Task BeforeCombatStart()
    {
        _triggeredEmberThisTurn = false;
        return base.BeforeCombatStart();
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredEmberThisTurn = false;
        return base.AfterCombatEnd(room);
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
            _triggeredEmberThisTurn = false;
        return base.AfterPlayerTurnStart(choiceContext, player);
    }

    protected override async Task OnFirstTurnStartAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature.CombatState is not { } combatState)
            return;

        Flash();
        var ember = combatState.CreateCard<Ember>(player);
        await CollectorMechanics.AddGeneratedCardToHandOrDiscardAsync(ember, player);
        if (player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await CollectorMechanics.GainReservesAsync(choiceContext, player, 1);
    }

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner != Owner
            || _triggeredEmberThisTurn
            || card is not Ember
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredEmberThisTurn = true;
        Flash();
        await CollectorMechanics.GainReservesAsync(choiceContext, Owner, 1);
    }
}
