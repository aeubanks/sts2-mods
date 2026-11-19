using System.Runtime.CompilerServices;
using BaseLib.Patches.Saves;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using Packmaster.Character;
using Packmaster.Config;

namespace Packmaster.Packs;

public static class PackRunState
{
    private const string SaveKey = "packmaster_active_packs";
    private static readonly ConditionalWeakTable<IRunState, HashSet<string>> _persistedByRun = new();
    private static bool _saveRegistered;

    public static void Initialize()
    {
        if (_saveRegistered)
            return;
        _saveRegistered = true;

        ExtendedSaveTypes.RegisterSavedValue<IRunState, string>(
            SaveKey,
            getter: runState => _persistedByRun.TryGetValue(runState, out var set) && set.Count > 0
                ? string.Join(",", set.OrderBy(id => id, StringComparer.Ordinal))
                : null,
            setter: (runState, raw) =>
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return;

                var validIds = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(id => PackRegistry.Get(id) is not null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (validIds.Count > 0)
                {
                    _persistedByRun.AddOrUpdate(runState, validIds);
                }
            },
            serializer: (val, writer) => writer.WriteString(val ?? string.Empty),
            deserializer: reader => reader.ReadString());
    }

    public static bool HasPersistedActivePacks(IRunState? state) =>
        state is not null && _persistedByRun.TryGetValue(state, out var set) && set.Count > 0;

    public static IReadOnlySet<string>? GetActivePackIdsForCurrentRun() =>
        GetActivePackIds(RunManager.Instance.DebugOnlyGetState());

    public static IReadOnlySet<string>? GetActivePackIds(IRunState? state)
    {
        if (PackmasterConfig.AllPacksMode || PackRegistry.AllPacks.Count == 0)
            return null;

        if (state is null || !state.Players.Any(p => p.Character is ThePackmaster))
            return null;

        if (_persistedByRun.TryGetValue(state, out var persisted) && persisted.Count > 0)
            return persisted;

        // Non-persisting deterministic fallback when queried before an explicit draft (e.g., debug/test combat).
        var rolled = RollPackIdsForSeed(state.Rng.StringSeed, PackmasterConfig.PacksPerRun);
        return new HashSet<string>(rolled, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> RollPackIdsForSeed(string seed, int targetCount)
    {
        var allPacks = PackRegistry.AllPacks;
        if (allPacks.Count == 0)
            return [];

        var chosen = new List<string>();
        var remaining = new List<PackDefinition>();

        foreach (var pack in allPacks)
        {
            if (pack.AlwaysInclude)
                chosen.Add(pack.Id);
            else
                remaining.Add(pack);
        }

        var rng = new Rng((uint)StringHelper.GetDeterministicHashCode(seed + "_PACKMASTER_PACKS"));
        while (chosen.Count < targetCount && remaining.Count > 0)
        {
            int idx = rng.NextInt(remaining.Count);
            chosen.Add(remaining[idx].Id);
            remaining.RemoveAt(idx);
        }

        return chosen;
    }

    public static void SetActivePacksForRun(IRunState state, IEnumerable<string> packIds)
    {
        var set = new HashSet<string>(packIds, StringComparer.OrdinalIgnoreCase);
        if (set.Count == 0)
        {
            _persistedByRun.Remove(state);
            return;
        }

        _persistedByRun.AddOrUpdate(state, set);
        MainFile.Logger.Info($"Persisted Packmaster active packs ({set.Count}): {string.Join(", ", set)}");
    }

    public static void Reset() => _persistedByRun.Clear();
}
