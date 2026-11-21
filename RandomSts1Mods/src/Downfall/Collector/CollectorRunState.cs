using System.Runtime.CompilerServices;
using Downfall.Collector.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector;

/// <summary>
/// Per-player persistent Collector state (Essence, Collection entries, Bottled Collection index).
/// Keyed by Player (multiplayer-safe) and lazily hydrated from / synced back to CollectorTorchRelic's
/// [SavedProperty] fields so save/load and Touch of Orobas replacement preserve everything seamlessly.
/// </summary>
public sealed class CollectorRunState
{
    public const int StartingEssence = 5;
    public const int CollectibleEssenceCost = 3;

    private static readonly ConditionalWeakTable<Player, CollectorRunState> States = new();

    public readonly record struct CollectionEntry(ModelId CardId, int UpgradeLevel);

    public int Essence { get; private set; } = StartingEssence;
    public int BottledCollectionIndex { get; private set; } = -1;
    private readonly List<CollectionEntry> _collection = [];

    public IReadOnlyList<CollectionEntry> Collection => _collection;

    public static CollectorRunState GetOrCreate(Player player)
    {
        if (States.TryGetValue(player, out var existing))
            return existing;

        var state = new CollectorRunState();
        var torch = player.Relics.OfType<CollectorTorchRelic>().FirstOrDefault();
        if (torch is not null)
        {
            state.HydrateFromTorch(torch);
        }
        States.Add(player, state);
        return state;
    }

    /// <summary>
    /// Strictly read-only accessor safe for HoverTips / UI without mutating state or [SavedProperty].
    /// </summary>
    public static CollectorRunState? TryGetReadOnly(Player? player)
    {
        if (player is null)
            return null;
        return States.TryGetValue(player, out var existing) ? existing : null;
    }

    /// <summary>
    /// Ensures a player's CollectorRunState exists and is hydrated from the provided torch instance
    /// (used in CollectorTorchRelic.AfterRemoved when the torch may already be detached from player.Relics).
    /// </summary>
    public static CollectorRunState EnsureHydratedFromTorch(Player player, CollectorTorchRelic torch)
    {
        if (States.TryGetValue(player, out var existing))
            return existing;

        var state = new CollectorRunState();
        state.HydrateFromTorch(torch);
        States.Add(player, state);
        return state;
    }

    public void HydrateFromTorch(CollectorTorchRelic torch)
    {
        Essence = Math.Max(0, torch.SavedEssence);
        BottledCollectionIndex = torch.SavedBottledIndex;
        _collection.Clear();
        ParseCsvInto(torch.SavedCollectionCsv, _collection);
        if (BottledCollectionIndex < 0 || BottledCollectionIndex >= _collection.Count)
            BottledCollectionIndex = -1;
    }

    public void SyncToTorch(Player player)
    {
        var torch = player.Relics.OfType<CollectorTorchRelic>().FirstOrDefault();
        if (torch is null || !torch.IsMutable)
            return;

        torch.SyncFromRunState(Essence, SerializeCsv(_collection), BottledCollectionIndex);
    }

    public void ChangeEssence(Player player, int delta)
    {
        Essence = Math.Max(0, Essence + delta);
        SyncToTorch(player);
    }

    public void SetEssence(Player player, int amount)
    {
        Essence = Math.Max(0, amount);
        SyncToTorch(player);
    }

    public void AddToCollection(Player player, CardModel card)
    {
        _collection.Add(new CollectionEntry(card.Id, card.CurrentUpgradeLevel));
        SyncToTorch(player);
    }

    public bool RemoveFromCollectionAt(Player player, int index)
    {
        if (index < 0 || index >= _collection.Count)
            return false;

        _collection.RemoveAt(index);

        if (BottledCollectionIndex == index)
            BottledCollectionIndex = -1;
        else if (index < BottledCollectionIndex)
            BottledCollectionIndex--;

        SyncToTorch(player);
        return true;
    }

    public bool UpgradeAndBottleAt(Player player, int index)
    {
        if (index < 0 || index >= _collection.Count)
            return false;

        var entry = _collection[index];
        var canonical = ModelDb.GetByIdOrNull<CardModel>(entry.CardId);
        int maxLevel = canonical?.MaxUpgradeLevel ?? 1;
        int nextLevel = Math.Min(maxLevel, entry.UpgradeLevel + 1);
        _collection[index] = entry with { UpgradeLevel = nextLevel };
        BottledCollectionIndex = index;
        SyncToTorch(player);
        return true;
    }

    public List<CardModel> CreateCollectionCardViews(Player player)
    {
        var list = new List<CardModel>(_collection.Count);
        foreach (var entry in _collection)
        {
            var canonical = ModelDb.GetByIdOrNull<CardModel>(entry.CardId);
            if (canonical is null)
                continue;

            var card = player.RunState.CreateCard(canonical, player);
            for (int u = 0; u < entry.UpgradeLevel && card.IsUpgradable; u++)
                card.UpgradeInternal();
            list.Add(card);
        }
        return list;
    }

    public static string SerializeCsv(IReadOnlyList<CollectionEntry> entries)
    {
        if (entries.Count == 0)
            return string.Empty;

        return string.Join(";", entries.Select(e => $"{e.CardId.Entry}:{e.UpgradeLevel}"));
    }

    public static void ParseCsvInto(string? csv, List<CollectionEntry> dest)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return;

        foreach (var part in csv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split(':');
            string entryKey = pieces[0];
            int upgradeLevel = pieces.Length > 1 && int.TryParse(pieces[1], out int parsed) ? Math.Max(0, parsed) : 0;
            var cardId = new ModelId("CARD", entryKey);
            if (ModelDb.GetByIdOrNull<CardModel>(cardId) is not null)
            {
                dest.Add(new CollectionEntry(cardId, upgradeLevel));
            }
        }
    }
}
