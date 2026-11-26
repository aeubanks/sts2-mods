using System.Runtime.CompilerServices;
using BaseLib.Patches.Saves;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using Packmaster.Character;
using Packmaster.Config;

namespace Packmaster.Packs;

public static class PackRunState
{
    private const string SaveKey = "packmaster_active_packs";

    private sealed class RunPackData
    {
        public Dictionary<ulong, HashSet<string>> ByPlayer { get; } = new();
        public HashSet<string>? LegacyRunWide { get; set; }
    }

    private static readonly ConditionalWeakTable<IRunState, RunPackData> _persistedByRun = new();
    private static bool _saveRegistered;

    [ThreadStatic]
    private static Stack<Player?>? _scopedPlayers;

    public static event Action<IRunState, Player?>? ActivePacksChanged;

    public static Player? CurrentScopedPlayer =>
        _scopedPlayers is { Count: > 0 } ? _scopedPlayers.Peek() : null;

    public static IDisposable ScopePlayer(Player? player)
    {
        _scopedPlayers ??= new Stack<Player?>();
        _scopedPlayers.Push(player);
        return new PlayerScopeCookie();
    }

    private sealed class PlayerScopeCookie : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_scopedPlayers is { Count: > 0 })
                _scopedPlayers.Pop();
        }
    }

    public static void Initialize()
    {
        if (_saveRegistered)
            return;
        _saveRegistered = true;

        ExtendedSaveTypes.RegisterSavedValue<IRunState, string>(
            SaveKey,
            getter: SerializeForRun,
            setter: DeserializeForRun,
            serializer: (val, writer) => writer.WriteString(val ?? string.Empty),
            deserializer: reader => reader.ReadString());
    }

    private static string? SerializeForRun(IRunState runState)
    {
        if (!_persistedByRun.TryGetValue(runState, out var data))
            return null;

        if (data.ByPlayer.Count > 0)
        {
            var segments = new List<string>(data.ByPlayer.Count);
            foreach (var (netId, set) in data.ByPlayer.OrderBy(kv => kv.Key))
            {
                if (set.Count == 0)
                    continue;
                segments.Add($"{netId}={string.Join(",", set.OrderBy(id => id, StringComparer.Ordinal))}");
            }
            if (segments.Count > 0)
                return string.Join(";", segments);
        }

        if (data.LegacyRunWide is { Count: > 0 })
            return string.Join(",", data.LegacyRunWide.OrderBy(id => id, StringComparer.Ordinal));

        return null;
    }

    private static void DeserializeForRun(IRunState runState, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var data = new RunPackData();
        if (raw.Contains('='))
        {
            foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0 || eq >= entry.Length - 1)
                    continue;

                if (!ulong.TryParse(entry[..eq], out ulong netId))
                    continue;

                var validIds = ParseValidIds(entry[(eq + 1)..]);
                if (validIds.Count > 0)
                {
                    data.ByPlayer[netId] = validIds;
                }
            }
        }
        else
        {
            var validIds = ParseValidIds(raw);
            if (validIds.Count > 0)
            {
                var firstPackmaster = runState.Players.FirstOrDefault(p => p.Character is ThePackmaster);
                if (firstPackmaster is not null)
                    data.ByPlayer[firstPackmaster.NetId] = validIds;
                else
                    data.LegacyRunWide = validIds;
            }
        }

        if (data.ByPlayer.Count > 0 || data.LegacyRunWide is { Count: > 0 })
        {
            _persistedByRun.AddOrUpdate(runState, data);
        }
    }

    private static HashSet<string> ParseValidIds(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => PackRegistry.Get(id) is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool HasPersistedActivePacks(Player? player)
    {
        if (player is null || player.RunState is not { } runState)
            return false;

        if (!_persistedByRun.TryGetValue(runState, out var data))
            return false;

        if (data.ByPlayer.TryGetValue(player.NetId, out var set) && set.Count > 0)
            return true;

        return data.LegacyRunWide is { Count: > 0 }
            && runState.Players.FirstOrDefault(p => p.Character is ThePackmaster) == player;
    }

    public static bool HasPersistedActivePacks(IRunState? state)
    {
        if (state is null || !_persistedByRun.TryGetValue(state, out var data))
            return false;

        return data.ByPlayer.Values.Any(s => s.Count > 0)
            || data.LegacyRunWide is { Count: > 0 };
    }

    public static IReadOnlySet<string>? GetActivePackIdsForCurrentRun() =>
        GetActivePackIds(RunManager.Instance.DebugOnlyGetState());

    public static IReadOnlySet<string>? GetActivePackIds(Player? player)
    {
        if (PackmasterConfig.AllPacksMode || PackRegistry.AllPacks.Count == 0)
            return null;

        if (player is null || player.Character is not ThePackmaster)
            return null;

        var state = player.RunState;
        if (state is null)
            return null;

        if (_persistedByRun.TryGetValue(state, out var data))
        {
            if (data.ByPlayer.TryGetValue(player.NetId, out var persisted) && persisted.Count > 0)
                return persisted;

            if (data.LegacyRunWide is { Count: > 0 }
                && state.Players.FirstOrDefault(p => p.Character is ThePackmaster) == player)
            {
                return data.LegacyRunWide;
            }
        }

        // Non-persisting deterministic fallback when queried before an explicit draft (e.g., debug/test combat).
        var rolled = RollPackIdsForSeed(GetFallbackSeed(state, player), PackmasterConfig.PacksPerRun);
        return new HashSet<string>(rolled, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlySet<string>? GetActivePackIds(IRunState? state)
    {
        if (PackmasterConfig.AllPacksMode || PackRegistry.AllPacks.Count == 0)
            return null;

        if (state is null || !state.Players.Any(p => p.Character is ThePackmaster))
            return null;

        var scoped = CurrentScopedPlayer;
        if (scoped is not null && scoped.Character is ThePackmaster && ReferenceEquals(scoped.RunState, state))
            return GetActivePackIds(scoped);

        var local = LocalContext.GetMe(state);
        if (local is not null && local.Character is ThePackmaster)
            return GetActivePackIds(local);

        var firstPackmaster = state.Players.FirstOrDefault(p => p.Character is ThePackmaster);
        return GetActivePackIds(firstPackmaster);
    }

    public static IReadOnlyList<PackDefinition> GetActivePackDefinitions(Player? player)
    {
        var allPacks = PackRegistry.AllPacks;
        if (allPacks.Count == 0)
            return [];

        var activeIds = GetActivePackIds(player);
        if (activeIds is null)
            return allPacks.OrderByDescending(p => p.AlwaysInclude).ToList();

        return allPacks
            .Where(p => activeIds.Contains(p.Id))
            .OrderByDescending(p => p.AlwaysInclude)
            .ToList();
    }

    public static string GetDraftSeed(IRunState state, Player player) =>
        state.Players.Count <= 1
            ? state.Rng.StringSeed + "_PACKMASTER_DRAFT"
            : $"{state.Rng.StringSeed}_PACKMASTER_DRAFT_{player.NetId}";

    private static string GetFallbackSeed(IRunState state, Player player) =>
        state.Players.Count <= 1
            ? state.Rng.StringSeed
            : $"{state.Rng.StringSeed}_P{player.NetId}";

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

    public static void SetActivePacksForPlayer(Player player, IEnumerable<string> packIds)
    {
        var state = player.RunState;
        var set = new HashSet<string>(packIds, StringComparer.OrdinalIgnoreCase);

        if (!_persistedByRun.TryGetValue(state, out var data))
        {
            if (set.Count == 0)
                return;
            data = new RunPackData();
            _persistedByRun.AddOrUpdate(state, data);
        }

        if (set.Count == 0)
        {
            data.ByPlayer.Remove(player.NetId);
            if (data.ByPlayer.Count == 0 && data.LegacyRunWide is null)
                _persistedByRun.Remove(state);
        }
        else
        {
            data.ByPlayer[player.NetId] = set;
            RandomSts1Mods.MainFile.Logger.Info(
                $"Persisted Packmaster active packs for player {player.NetId} ({set.Count}): {string.Join(", ", set)}");
        }

        ActivePacksChanged?.Invoke(state, player);
    }

    public static void SetActivePacksForRun(IRunState state, IEnumerable<string> packIds)
    {
        var firstPackmaster = state.Players.FirstOrDefault(p => p.Character is ThePackmaster);
        if (firstPackmaster is not null)
        {
            SetActivePacksForPlayer(firstPackmaster, packIds);
            return;
        }

        var set = new HashSet<string>(packIds, StringComparer.OrdinalIgnoreCase);
        if (set.Count == 0)
        {
            _persistedByRun.Remove(state);
            ActivePacksChanged?.Invoke(state, null);
            return;
        }

        var data = _persistedByRun.GetOrCreateValue(state);
        data.LegacyRunWide = set;
        RandomSts1Mods.MainFile.Logger.Info($"Persisted Packmaster active packs ({set.Count}): {string.Join(", ", set)}");
        ActivePacksChanged?.Invoke(state, null);
    }

    public static void Reset() => _persistedByRun.Clear();
}
