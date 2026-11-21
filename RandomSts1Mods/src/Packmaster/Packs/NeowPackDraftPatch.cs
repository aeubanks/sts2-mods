using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Random;
using Packmaster.Character;
using Packmaster.Config;

namespace Packmaster.Packs;

[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
internal static class NeowPackDraftPatch
{
    private const string DraftKeyPrefix = "PACKMASTER_DRAFT_";
    private const int MaxChoicePicks = 3;
    private const int CandidatesPerPick = 3;

    private static readonly Action<EventModel, LocString, IEnumerable<EventOption>> SetEventState =
        AccessTools.MethodDelegate<Action<EventModel, LocString, IEnumerable<EventOption>>>(
            AccessTools.Method(typeof(EventModel), "SetEventState", [typeof(LocString), typeof(IEnumerable<EventOption>)])
            ?? throw new MissingMethodException(nameof(EventModel), "SetEventState"));

    private static readonly Func<EventModel, IReadOnlyList<EventOption>> GenerateInitialOptionsWrapper =
        AccessTools.MethodDelegate<Func<EventModel, IReadOnlyList<EventOption>>>(
            AccessTools.Method(typeof(EventModel), "GenerateInitialOptionsWrapper")
            ?? throw new MissingMethodException(nameof(EventModel), "GenerateInitialOptionsWrapper"));

    [HarmonyPrefix]
    private static bool Prefix(Neow __instance, ref IReadOnlyList<EventOption> __result)
    {
        var owner = __instance.Owner;
        if (owner?.Character is not ThePackmaster || PackmasterConfig.AllPacksMode)
            return true;

        var runState = owner.RunState;
        // In co-op with multiple Packmasters, only the first Packmaster runs the shared run-wide draft.
        if (runState.Players.FirstOrDefault(p => p.Character is ThePackmaster) != owner)
            return true;

        if (PackRunState.HasPersistedActivePacks(runState))
            return true;

        var allPacks = PackRegistry.AllPacks;
        if (allPacks.Count == 0)
            return true;

        var chosen = new List<string>();
        var remaining = new List<PackDefinition>();
        foreach (var pack in allPacks)
        {
            if (pack.AlwaysInclude)
                chosen.Add(pack.Id);
            else
                remaining.Add(pack);
        }

        int targetCount = Math.Min(PackmasterConfig.PacksPerRun, allPacks.Count);
        int slotsToFill = targetCount - chosen.Count;
        if (slotsToFill <= 0 || remaining.Count <= slotsToFill)
        {
            PackRunState.SetActivePacksForRun(runState, slotsToFill <= 0 ? chosen : allPacks.Select(p => p.Id));
            return true;
        }

        var rng = new Rng((uint)StringHelper.GetDeterministicHashCode(runState.Rng.StringSeed + "_PACKMASTER_DRAFT"));
        int choicePicks = Math.Min(MaxChoicePicks, slotsToFill);
        int randomPicks = slotsToFill - choicePicks;

        for (int i = 0; i < randomPicks && remaining.Count > 0; i++)
        {
            int idx = rng.NextInt(remaining.Count);
            chosen.Add(remaining[idx].Id);
            remaining.RemoveAt(idx);
        }

        __result = BuildDraftOptions(__instance, chosen, remaining, rng, pickIndex: 1, totalPicks: choicePicks);
        return false;
    }

    private static IReadOnlyList<EventOption> BuildDraftOptions(
        Neow neow,
        List<string> chosen,
        List<PackDefinition> remaining,
        Rng rng,
        int pickIndex,
        int totalPicks)
    {
        var candidates = SampleCandidates(remaining, rng, Math.Min(CandidatesPerPick, remaining.Count));
        string activeNames = FormatActivePackNames(chosen);
        var options = new List<EventOption>(candidates.Count);

        foreach (var pack in candidates)
        {
            var title = pack.Title;
            var desc = new LocString("gameplay_ui", "PACKMASTER-DRAFT_OPTION.description");
            desc.Add("PackDesc", pack.Description.Exists() ? pack.Description.GetFormattedText() : pack.Id);
            desc.Add("Pick", pickIndex);
            desc.Add("Total", totalPicks);
            desc.Add("ActiveList", activeNames);

            var pickedPack = pack;
            var option = new EventOption(
                neow,
                () => OnPackPicked(neow, chosen, remaining, rng, pickedPack, pickIndex, totalPicks),
                title,
                desc,
                $"{DraftKeyPrefix}{pickIndex}_{pack.Id}",
                Array.Empty<IHoverTip>())
                .ThatWontSaveToChoiceHistory();

            options.Add(option);
        }

        return options;
    }

    private static Task OnPackPicked(
        Neow neow,
        List<string> chosen,
        List<PackDefinition> remaining,
        Rng rng,
        PackDefinition pickedPack,
        int pickIndex,
        int totalPicks)
    {
        if (!remaining.Remove(pickedPack))
            return Task.CompletedTask;

        chosen.Add(pickedPack.Id);

        if (pickIndex < totalPicks && remaining.Count > 0)
        {
            int nextPick = pickIndex + 1;
            var pageDesc = new LocString("gameplay_ui", "PACKMASTER-DRAFT_PAGE.description");
            pageDesc.Add("Pick", nextPick);
            pageDesc.Add("Total", totalPicks);
            pageDesc.Add("ActiveList", FormatActivePackNames(chosen));

            var nextOptions = BuildDraftOptions(neow, chosen, remaining, rng, nextPick, totalPicks);
            SetEventState(neow, pageDesc, nextOptions);
            return Task.CompletedTask;
        }

        // Draft complete: persist chosen packs, then let vanilla Neow generate fresh blessings using the drafted pool!
        PackRunState.SetActivePacksForRun(neow.Owner!.RunState, chosen);
        var neowOptions = GenerateInitialOptionsWrapper(neow);
        SetEventState(neow, neow.InitialDescription, neowOptions);
        return Task.CompletedTask;
    }

    private static List<PackDefinition> SampleCandidates(List<PackDefinition> pool, Rng rng, int count)
    {
        var copy = pool.ToList();
        var result = new List<PackDefinition>(count);
        for (int i = 0; i < count && copy.Count > 0; i++)
        {
            int idx = rng.NextInt(copy.Count);
            result.Add(copy[idx]);
            copy.RemoveAt(idx);
        }
        return result;
    }

    internal static bool TryExtractDraftPackId(string? textKey, out string packId)
    {
        packId = string.Empty;
        if (string.IsNullOrEmpty(textKey) || !textKey.StartsWith(DraftKeyPrefix, StringComparison.Ordinal))
            return false;

        int sep = textKey.IndexOf('_', DraftKeyPrefix.Length);
        if (sep <= DraftKeyPrefix.Length || sep + 1 >= textKey.Length)
            return false;

        packId = textKey[(sep + 1)..];
        return true;
    }

    private static string FormatActivePackNames(List<string> packIds)
    {
        if (packIds.Count == 0)
            return "None";

        return string.Join(", ", packIds.Select(id =>
        {
            var pack = PackRegistry.Get(id);
            return pack is not null && pack.Title.Exists() ? pack.Title.GetFormattedText() : id;
        }));
    }
}

[HarmonyPatch(typeof(NEventOptionButton), "_Ready")]
internal static class NeowPackDraftButtonIconPatch
{
    [HarmonyPostfix]
    private static void Postfix(NEventOptionButton __instance)
    {
        if (__instance.Event is not Neow)
            return;

        if (!NeowPackDraftPatch.TryExtractDraftPackId(__instance.Option?.TextKey, out var packId))
            return;

        var pack = PackRegistry.Get(packId);
        if (pack is null || !ResourceLoader.Exists(pack.IconPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(pack.IconPath);
        if (texture is null)
            return;

        var icon = __instance.GetNodeOrNull<TextureRect>("%RelicIcon");
        if (icon is null)
            return;

        icon.Texture = texture;
        var outline = icon.GetNodeOrNull<TextureRect>("%Outline");
        if (outline is not null)
        {
            outline.Texture = null;
            outline.Visible = false;
        }
        icon.Visible = true;
    }
}
