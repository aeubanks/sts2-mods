using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using Packmaster.Character;
using Packmaster.Config;

namespace Packmaster.Packs;

public static class PackRunState
{
    private static readonly ConditionalWeakTable<RunState, HashSet<string>> _byRun = new();

    public static IReadOnlySet<string>? GetActivePackIdsForCurrentRun()
    {
        if (PackmasterConfig.AllPacksMode || PackRegistry.AllPacks.Count == 0)
            return null;

        var state = RunManager.Instance.DebugOnlyGetState();
        if (state is null || !state.Players.Any(p => p.Character is ThePackmaster))
            return null;

        return _byRun.GetValue(state, static runState =>
        {
            var rolled = RollPackIdsForSeed(runState.Rng.StringSeed, PackmasterConfig.PacksPerRun);
            MainFile.Logger.Info($"Active Packmaster packs ({rolled.Count}): {string.Join(", ", rolled)}");
            return new HashSet<string>(rolled, StringComparer.OrdinalIgnoreCase);
        });
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

    public static void SetActivePacksForRun(RunState state, IEnumerable<string> packIds)
    {
        _byRun.AddOrUpdate(state, new HashSet<string>(packIds, StringComparer.OrdinalIgnoreCase));
    }

    public static void Reset() => _byRun.Clear();
}
