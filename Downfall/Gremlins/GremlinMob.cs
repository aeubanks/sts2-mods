using System.Runtime.CompilerServices;
using System.Text;
using Downfall.Core;
using Downfall.Gremlins.Cards;
using Downfall.Gremlins.Powers;
using Downfall.Gremlins.Relics;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins;

public enum GremlinKind
{
    Mad = 0,
    Fat = 1,
    Shield = 2,
    Sneaky = 3,
    Wizard = 4
}

public interface IGremlinSwapListener
{
    Task OnGremlinSwappedAsync(PlayerChoiceContext choiceContext, GremlinKind previous, GremlinKind current);
}

public interface IGremlinWizConsumedListener
{
    Task OnWizConsumedAsync(PlayerChoiceContext choiceContext);
}

public interface IGremlinWizBonusProvider
{
    decimal BonusWizDamage { get; }
}

public interface IRandomDamageBonusProvider
{
    decimal BonusRandomEnemyDamage { get; }
}

public interface IGremlinSacrificeProvider
{
    GremlinKind? SacrificedGremlin { get; }
}

internal static class GremlinMob
{
    public const int SacrificedHp = -1;
    public const string StandbyRowNodeName = "GremlinStandbyRow";
    public const string StandbyHoverLayerNodeName = "GremlinStandbyHoverLayer";

    private sealed class RunMobState
    {
        public GremlinKind[] Order = new GremlinKind[5];
        public int[] Hp = new int[5];
        public bool InCombat;
        public bool IsNob;
    }

    private static readonly ConditionalWeakTable<Player, RunMobState> RunStates = new();
    private static readonly ConditionalWeakTable<Node2D, Creature[]> StandbyHealthProxies = new();
    private const float StandbyHealthBarWidth = 108f;
    private const float StandbyHealthBarScale = 0.68f;

    public static readonly GremlinKind[] AllKinds =
    [
        GremlinKind.Mad,
        GremlinKind.Fat,
        GremlinKind.Shield,
        GremlinKind.Sneaky,
        GremlinKind.Wizard
    ];

    public const string NobSpriteTexturePath = "res://Downfall/images/gremlins/char/nob.png";

    private static readonly Vector2[] StandbySlotOffsets =
    [
        new(-92f, 16f),
        new(-180f, 12f),
        new(-118f, -134f),
        new(-206f, -130f)
    ];

    // Add back row (Slots 3, 2) BEFORE front row (Slots 1, 0) for consistent draw & hover order.
    private static readonly int[] StandbyDrawOrder = [3, 2, 1, 0];

    public static string SpriteTexturePathFor(GremlinKind kind) => kind switch
    {
        GremlinKind.Mad => "res://Downfall/images/gremlins/char/angry.png",
        GremlinKind.Fat => "res://Downfall/images/gremlins/char/fat.png",
        GremlinKind.Shield => "res://Downfall/images/gremlins/char/shield.png",
        GremlinKind.Sneaky => "res://Downfall/images/gremlins/char/sneak.png",
        GremlinKind.Wizard => "res://Downfall/images/gremlins/char/wizard.png",
        _ => "res://Downfall/images/gremlins/char/gremlins.png"
    };

    public static string DisplayNameFor(GremlinKind kind) => kind switch
    {
        GremlinKind.Mad => "Mad",
        GremlinKind.Fat => "Fat",
        GremlinKind.Shield => "Shield",
        GremlinKind.Sneaky => "Sneaky",
        GremlinKind.Wizard => "Wizard",
        _ => "Gremlin"
    };

    private static (string PowerKey, int BaseAmount) PowerLocInfoFor(GremlinKind kind) => kind switch
    {
        GremlinKind.Mad => ("DOWNFALL-MAD_GREMLIN_POWER", 2),
        GremlinKind.Fat => ("DOWNFALL-FAT_GREMLIN_POWER", 1),
        GremlinKind.Shield => ("DOWNFALL-SHIELD_GREMLIN_POWER", 2),
        GremlinKind.Sneaky => ("DOWNFALL-SNEAKY_GREMLIN_POWER", 2),
        GremlinKind.Wizard => ("DOWNFALL-GREMLIN_WIZARD_POWER", 1),
        _ => ("DOWNFALL-MAD_GREMLIN_POWER", 2)
    };

    private static RunMobState? TryGetStateReadOnly(Player player, int maxHp)
    {
        if (RunStates.TryGetValue(player, out var cached))
            return cached;

        string? saved = player.Relics.OfType<GremlinKnobBase>().FirstOrDefault()?.MobStateEncoded;
        return TryDecodeState(saved, maxHp, out var decoded) ? decoded : null;
    }

    public static HoverTip BuildSingleGremlinHoverTip(Player player, GremlinKind kind)
    {
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        var state = TryGetStateReadOnly(player, maxHp);
        int hp = state is null
            ? Math.Clamp(player.Creature.CurrentHp, 0, maxHp)
            : (kind == state.Order[0] && state.Hp[(int)kind] > 0 && player.Creature.CurrentHp > 0
                ? Math.Clamp(player.Creature.CurrentHp, 0, maxHp)
                : (state.Hp[(int)kind] > 0 ? Math.Min(state.Hp[(int)kind], maxHp) : state.Hp[(int)kind]));

        string hpLine = hp switch
        {
            SacrificedHp => "[red]Lost[/red]",
            0 => "[red]Dead[/red]",
            _ => $"HP: [blue]{hp}/{maxHp}[/blue]"
        };

        (string powerKey, int baseAmount) = PowerLocInfoFor(kind);
        var powerDesc = new LocString("powers", $"{powerKey}.smartDescription");
        powerDesc.Add("Amount", baseAmount);
        string desc = $"{hpLine}\n{powerDesc.GetFormattedText()}";
        return new HoverTip(new LocString("powers", $"{powerKey}.title"), desc);
    }

    public static bool IsValidGremlinSpritePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        if (path == "res://Downfall/images/gremlins/char/gremlins.png" || path == NobSpriteTexturePath)
            return true;
        foreach (var k in AllKinds)
        {
            if (path == SpriteTexturePathFor(k))
                return true;
        }
        return false;
    }

    internal static void InvalidateCache(Player player) => RunStates.Remove(player);

    private static string EncodeState(RunMobState state) =>
        $"{(int)state.Order[0]},{(int)state.Order[1]},{(int)state.Order[2]},{(int)state.Order[3]},{(int)state.Order[4]}" +
        $"|{state.Hp[0]},{state.Hp[1]},{state.Hp[2]},{state.Hp[3]},{state.Hp[4]}";

    private static bool TryDecodeState(string? encoded, int maxHp, out RunMobState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(encoded))
            return false;

        var halves = encoded.Split('|');
        if (halves.Length != 2)
            return false;

        var orderParts = halves[0].Split(',');
        var hpParts = halves[1].Split(',');
        if (orderParts.Length != 5 || hpParts.Length != 5)
            return false;

        var parsed = new RunMobState();
        var seen = new bool[5];
        for (int i = 0; i < 5; i++)
        {
            if (!int.TryParse(orderParts[i], out int kindIdx) || kindIdx < 0 || kindIdx >= 5 || seen[kindIdx])
                return false;
            seen[kindIdx] = true;
            parsed.Order[i] = (GremlinKind)kindIdx;

            if (!int.TryParse(hpParts[i], out int hp) || hp < SacrificedHp)
                return false;
            parsed.Hp[i] = hp > 0 ? Math.Min(hp, Math.Max(1, maxHp)) : hp;
        }

        state = parsed;
        return true;
    }

    private static void SyncToRelic(Player player, RunMobState state)
    {
        if (player.Relics.OfType<GremlinKnobBase>().FirstOrDefault() is { } knob)
        {
            knob.MobStateEncoded = EncodeState(state);
        }
    }

    private static RunMobState GetOrCreateState(Player player)
    {
        if (RunStates.TryGetValue(player, out var existing))
            return existing;

        int maxHp = Math.Max(1, player.Creature.MaxHp);
        string? saved = player.Relics.OfType<GremlinKnobBase>().FirstOrDefault()?.MobStateEncoded;
        if (TryDecodeState(saved, maxHp, out var decoded) && decoded is not null)
        {
            ApplySacrificeProviders(player, decoded);
            EnsureFrontIsLiving(decoded);
            RunStates.AddOrUpdate(player, decoded);
            return decoded;
        }

        var state = new RunMobState();
        var order = AllKinds.ToArray();
        var rng = player.RunState.Rng.CombatCardGeneration;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        state.Order = order;
        for (int i = 0; i < 5; i++)
        {
            state.Hp[i] = maxHp;
        }

        ApplySacrificeProviders(player, state);
        EnsureFrontIsLiving(state);

        if (player.Creature.CurrentHp > 0 && state.Hp[(int)state.Order[0]] > 0)
        {
            state.Hp[(int)state.Order[0]] = Math.Clamp(player.Creature.CurrentHp, 1, maxHp);
        }

        RunStates.AddOrUpdate(player, state);
        SyncToRelic(player, state);
        return state;
    }

    private static void ApplySacrificeProviders(Player player, RunMobState state)
    {
        foreach (var relic in player.Relics.OfType<IGremlinSacrificeProvider>())
        {
            if (relic.SacrificedGremlin is { } kind)
            {
                state.Hp[(int)kind] = SacrificedHp;
            }
        }
    }

    private static void EnsureFrontIsLiving(RunMobState state)
    {
        if (state.Hp[(int)state.Order[0]] > 0)
            return;

        for (int i = 1; i < 5; i++)
        {
            if (state.Hp[(int)state.Order[i]] > 0)
            {
                RotateToFront(state, state.Order[i]);
                return;
            }
        }
    }

    private static void RotateToFront(RunMobState state, GremlinKind next)
    {
        GremlinKind previous = state.Order[0];
        if (previous == next)
            return;

        var list = state.Order.ToList();
        list.Remove(next);
        list.RemoveAt(0);
        list.Insert(0, next);
        list.Add(previous);
        state.Order = list.ToArray();
    }

    public static bool IsNob(ICombatState? combatState, Player? player) =>
        player is not null && GetOrCreateState(player).IsNob;

    public static GremlinKind GetActiveKind(ICombatState? combatState, Player? player) =>
        player is not null ? GetOrCreateState(player).Order[0] : GremlinKind.Mad;

    public static IReadOnlyList<GremlinKind> GetStandbyKinds(ICombatState? combatState, Player? player)
    {
        if (player is null)
            return [];
        var state = GetOrCreateState(player);
        return state.Order.Skip(1).Where(k => state.Hp[(int)k] > 0).ToList();
    }

    public static int GetLivingCount(ICombatState? combatState, Player? player)
    {
        if (player is null)
            return 5;
        var state = GetOrCreateState(player);
        return state.Order.Count(k => state.Hp[(int)k] > 0);
    }

    public static int GetGremlinHp(Player player, GremlinKind kind)
    {
        var state = GetOrCreateState(player);
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        if (kind == state.Order[0] && state.Hp[(int)kind] > 0 && player.Creature.CurrentHp > 0)
        {
            return Math.Clamp(player.Creature.CurrentHp, 0, maxHp);
        }
        int hp = state.Hp[(int)kind];
        return hp > 0 ? Math.Min(hp, maxHp) : hp;
    }

    public static bool CanResurrect(Player player)
    {
        var state = GetOrCreateState(player);
        return state.Hp.Any(hp => hp == 0);
    }

    public static bool ShouldPreventDeath(Player player)
    {
        if (player.Character is not TheGremlins)
            return false;
        var state = GetOrCreateState(player);
        return state.Order.Skip(1).Any(k => state.Hp[(int)k] > 0);
    }

    public static void SyncFrontHpFromCreature(Player player)
    {
        var state = GetOrCreateState(player);
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        if (state.Hp[(int)state.Order[0]] >= 0)
        {
            state.Hp[(int)state.Order[0]] = Math.Clamp(player.Creature.CurrentHp, 0, maxHp);
        }
        SyncToRelic(player, state);
    }

    public static HoverTip BuildMobStatusHoverTip(Player player)
    {
        int maxHp = Math.Max(1, player.Creature.MaxHp);

        // Strictly read-only: never consume gameplay RNG or mutate [SavedProperty] from a UI HoverTip getter!
        var state = TryGetStateReadOnly(player, maxHp);

        var sb = new StringBuilder();
        if (state is null)
        {
            var sacrificed = player.Relics
                .OfType<IGremlinSacrificeProvider>()
                .Where(r => r.SacrificedGremlin is not null)
                .Select(r => r.SacrificedGremlin!.Value)
                .ToHashSet();
            int frontHp = Math.Clamp(player.Creature.CurrentHp, 0, maxHp);
            for (int i = 0; i < AllKinds.Length; i++)
            {
                GremlinKind kind = AllKinds[i];
                if (i > 0)
                    sb.Append('\n');
                string name = DisplayNameFor(kind);
                if (sacrificed.Contains(kind))
                {
                    sb.Append($"{name}: [red]Lost[/red]");
                }
                else
                {
                    sb.Append($"{name}: [blue]{frontHp}/{maxHp}[/blue] HP");
                }
            }
        }
        else
        {
            for (int i = 0; i < 5; i++)
            {
                GremlinKind kind = state.Order[i];
                int rawHp = i == 0 && state.Hp[(int)kind] > 0 && player.Creature.CurrentHp > 0
                    ? Math.Clamp(player.Creature.CurrentHp, 0, maxHp)
                    : (state.Hp[(int)kind] > 0 ? Math.Min(state.Hp[(int)kind], maxHp) : state.Hp[(int)kind]);
                if (i > 0)
                    sb.Append('\n');

                string name = DisplayNameFor(kind);
                if (rawHp == SacrificedHp)
                {
                    sb.Append($"{name}: [red]Lost[/red]");
                }
                else if (rawHp == 0)
                {
                    sb.Append($"{name}: [red]Dead[/red]");
                }
                else if (i == 0)
                {
                    sb.Append($"[gold]{name}[/gold]: [green]{rawHp}/{maxHp}[/green] HP (Active)");
                }
                else
                {
                    sb.Append($"{name}: [blue]{rawHp}/{maxHp}[/blue] HP");
                }
            }
        }

        return new HoverTip(new LocString("relics", "DOWNFALL-GREMLIN_MOB_STATUS.title"), sb.ToString());
    }

    public static async Task EnsureInitializedAsync(
        ICombatState combatState,
        Player player,
        PlayerChoiceContext choiceContext)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var state = GetOrCreateState(player);
        int maxHp = Math.Max(1, player.Creature.MaxHp);

        if (!state.InCombat)
        {
            state.InCombat = true;
            state.IsNob = false;
            ApplySacrificeProviders(player, state);

            for (int i = 0; i < 5; i++)
            {
                if (state.Hp[i] > maxHp)
                    state.Hp[i] = maxHp;
            }

            if (state.Hp[(int)state.Order[0]] > 0 && player.Creature.CurrentHp > 0)
            {
                state.Hp[(int)state.Order[0]] = Math.Clamp(player.Creature.CurrentHp, 1, maxHp);
            }
            else
            {
                EnsureFrontIsLiving(state);
                int frontHp = state.Hp[(int)state.Order[0]];
                if (frontHp > 0 && player.Creature.CurrentHp != frontHp)
                {
                    await CreatureCmd.SetCurrentHp(player.Creature, frontHp);
                }
            }

            SyncToRelic(player, state);
        }

        if (!state.IsNob && !player.Creature.Powers.OfType<GremlinActivePower>().Any())
        {
            await ApplyActiveGremlinPowerAsync(player, state.Order[0], choiceContext);
        }

        UpdateCombatVisuals(player);
    }

    public static void OnCombatEnd(Player player)
    {
        var state = GetOrCreateState(player);
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        if (state.Hp[(int)state.Order[0]] >= 0)
        {
            state.Hp[(int)state.Order[0]] = Math.Clamp(player.Creature.CurrentHp, 0, maxHp);
        }
        state.InCombat = false;
        state.IsNob = false;
        SyncToRelic(player, state);
    }

    public static async Task HandleActiveGremlinDeathAsync(Player player)
    {
        if (player.Creature.CurrentHp > 0)
            return;

        var state = GetOrCreateState(player);
        GremlinKind? nextLiving = null;
        for (int i = 1; i < 5; i++)
        {
            if (state.Hp[(int)state.Order[i]] > 0)
            {
                nextLiving = state.Order[i];
                break;
            }
        }

        if (nextLiving is null)
            return;

        if (state.IsNob)
        {
            state.IsNob = false;
            foreach (var nobPower in player.Creature.Powers.OfType<GremlinNobPower>().ToList())
            {
                await PowerCmd.Remove(nobPower);
            }
        }

        if (player.Creature.CombatState is not null)
        {
            await SwapInternalAsync(
                player,
                state,
                nextLiving.Value,
                new ThrowingPlayerChoiceContext(),
                isDeathSwap: true);
        }
        else
        {
            GremlinKind dead = state.Order[0];
            state.Hp[(int)dead] = 0;
            RotateToFront(state, nextLiving.Value);
            int nextHp = Math.Clamp(state.Hp[(int)nextLiving.Value], 1, Math.Max(1, player.Creature.MaxHp));
            state.Hp[(int)nextLiving.Value] = nextHp;
            SyncToRelic(player, state);
            await CreatureCmd.SetCurrentHp(player.Creature, nextHp);
        }
    }

    public static async Task<bool> SwapNextAsync(
        Player player,
        ICombatState combatState,
        PlayerChoiceContext choiceContext)
    {
        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetOrCreateState(player);
        if (state.IsNob || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        GremlinKind? next = null;
        for (int i = 1; i < 5; i++)
        {
            if (state.Hp[(int)state.Order[i]] > 0)
            {
                next = state.Order[i];
                break;
            }
        }

        return next is not null && await SwapInternalAsync(player, state, next.Value, choiceContext);
    }

    public static async Task<bool> SwapToSpecificAsync(
        Player player,
        ICombatState combatState,
        GremlinKind target,
        PlayerChoiceContext choiceContext)
    {
        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetOrCreateState(player);
        if (state.IsNob || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        if (state.Order[0] == target || state.Hp[(int)target] <= 0)
            return false;

        return await SwapInternalAsync(player, state, target, choiceContext);
    }

    public static async Task<bool> SwapRandomAsync(
        Player player,
        ICombatState combatState,
        PlayerChoiceContext choiceContext)
    {
        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetOrCreateState(player);
        if (state.IsNob || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        var livingStandby = state.Order.Skip(1).Where(k => state.Hp[(int)k] > 0).ToList();
        if (livingStandby.Count == 0)
            return false;

        int pick = player.RunState.Rng.CombatCardGeneration.NextInt(livingStandby.Count);
        return await SwapInternalAsync(player, state, livingStandby[pick], choiceContext);
    }

    public static async Task<bool> ChooseGremlinToSwapAsync(
        Player player,
        ICombatState combatState,
        PlayerChoiceContext choiceContext)
    {
        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetOrCreateState(player);
        if (state.IsNob || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        var livingStandby = state.Order.Skip(1).Where(k => state.Hp[(int)k] > 0).ToList();
        if (livingStandby.Count == 0)
            return false;

        if (livingStandby.Count == 1)
        {
            return await SwapInternalAsync(player, state, livingStandby[0], choiceContext);
        }

        var choices = new List<CardModel>(livingStandby.Count);
        foreach (var kind in livingStandby)
        {
            choices.Add(CreateChoiceCard(kind, combatState, player));
        }

        var prompt = new LocString("cards", "DOWNFALL-TAG_TEAM.selectionScreenPrompt");
        var prefs = new CardSelectorPrefs(prompt, 1);
        var picked = (await CardSelectCmd.FromSimpleGrid(choiceContext, choices, player, prefs)).FirstOrDefault();
        if (picked is not GremlinChoiceCard choiceCard)
            return false;

        return await SwapToSpecificAsync(player, combatState, choiceCard.Kind, choiceContext);
    }

    private static CardModel CreateChoiceCard(GremlinKind kind, ICombatState combatState, Player player) => kind switch
    {
        GremlinKind.Mad => combatState.CreateCard<MadGremlinChoice>(player),
        GremlinKind.Fat => combatState.CreateCard<FatGremlinChoice>(player),
        GremlinKind.Shield => combatState.CreateCard<ShieldGremlinChoice>(player),
        GremlinKind.Sneaky => combatState.CreateCard<SneakyGremlinChoice>(player),
        GremlinKind.Wizard => combatState.CreateCard<GremlinWizardChoice>(player),
        _ => combatState.CreateCard<MadGremlinChoice>(player)
    };

    private static async Task<bool> SwapInternalAsync(
        Player player,
        RunMobState state,
        GremlinKind next,
        PlayerChoiceContext choiceContext,
        bool isDeathSwap = false)
    {
        GremlinKind previous = state.Order[0];
        if (previous == next || state.Hp[(int)next] <= 0)
            return false;

        int maxHp = Math.Max(1, player.Creature.MaxHp);
        state.Hp[(int)previous] = isDeathSwap
            ? 0
            : Math.Clamp(player.Creature.CurrentHp, 1, maxHp);

        RotateToFront(state, next);

        int nextHp = Math.Clamp(state.Hp[(int)next], 1, maxHp);
        state.Hp[(int)next] = nextHp;
        SyncToRelic(player, state);

        // Must restore CurrentHp FIRST so player.Creature.IsAlive becomes true on death-swap!
        if (player.Creature.CurrentHp != nextHp)
        {
            await CreatureCmd.SetCurrentHp(player.Creature, nextHp);
        }

        await RemoveActiveGremlinPowersAsync(player);
        await ApplyActiveGremlinPowerAsync(player, next, choiceContext);
        UpdateCombatVisuals(player);

        foreach (var listener in player.Creature.Powers.OfType<IGremlinSwapListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return true;
            await listener.OnGremlinSwappedAsync(choiceContext, previous, next);
        }

        foreach (var listener in player.Relics.OfType<IGremlinSwapListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return true;
            await listener.OnGremlinSwappedAsync(choiceContext, previous, next);
        }

        if (player.PlayerCombatState is { } pcs)
        {
            foreach (var listener in pcs.Hand.Cards.OfType<IGremlinSwapListener>().ToList())
            {
                if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    return true;
                await listener.OnGremlinSwappedAsync(choiceContext, previous, next);
            }
        }

        return true;
    }

    public static void HealStandbyGremlins(Player player, int amount)
    {
        if (amount <= 0)
            return;
        var state = GetOrCreateState(player);
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        for (int i = 1; i < 5; i++)
        {
            int idx = (int)state.Order[i];
            if (state.Hp[idx] > 0)
            {
                state.Hp[idx] = Math.Min(maxHp, state.Hp[idx] + amount);
            }
        }
        SyncToRelic(player, state);
        UpdateCombatVisuals(player);
    }

    public static async Task HealAllToFullAndResurrectAllAsync(Player player)
    {
        var state = GetOrCreateState(player);
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        for (int i = 0; i < 5; i++)
        {
            if (state.Hp[i] >= 0)
            {
                state.Hp[i] = maxHp;
            }
        }
        SyncToRelic(player, state);
        if (player.Creature.IsAlive && player.Creature.CurrentHp < maxHp)
        {
            await CreatureCmd.SetCurrentHp(player.Creature, maxHp);
        }
        UpdateCombatVisuals(player);
    }

    public static GremlinKind? ResurrectRandom(Player player, int hp, bool preferCombatRng = true)
    {
        var state = GetOrCreateState(player);
        var dead = state.Order.Where(k => state.Hp[(int)k] == 0).ToList();
        if (dead.Count == 0)
            return null;

        var rng = preferCombatRng ? player.RunState.Rng.CombatCardGeneration : player.RunState.Rng.Niche;
        GremlinKind picked = dead[rng.NextInt(dead.Count)];
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        state.Hp[(int)picked] = Math.Clamp(hp, 1, maxHp);
        SyncToRelic(player, state);
        UpdateCombatVisuals(player);
        return picked;
    }

    public static async Task SacrificeAsync(
        Player player,
        GremlinKind kind,
        PlayerChoiceContext choiceContext)
    {
        var state = GetOrCreateState(player);
        if (state.Hp[(int)kind] == SacrificedHp)
            return;

        int livingCount = state.Order.Count(k => state.Hp[(int)k] > 0);
        if (state.Hp[(int)kind] > 0 && livingCount <= 1)
            return;

        if (state.Order[0] == kind)
        {
            GremlinKind? next = null;
            for (int i = 1; i < 5; i++)
            {
                if (state.Hp[(int)state.Order[i]] > 0)
                {
                    next = state.Order[i];
                    break;
                }
            }

            if (next is null)
                return;

            if (player.Creature.CombatState is not null && !state.IsNob && player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await SwapInternalAsync(player, state, next.Value, choiceContext);
            }
            else
            {
                RotateToFront(state, next.Value);
                if (!state.IsNob && player.Creature.IsAlive)
                {
                    int nextHp = Math.Clamp(state.Hp[(int)next.Value], 1, Math.Max(1, player.Creature.MaxHp));
                    await CreatureCmd.SetCurrentHp(player.Creature, nextHp);
                }
            }
        }

        state.Hp[(int)kind] = SacrificedHp;
        SyncToRelic(player, state);
        UpdateCombatVisuals(player);
    }

    public static async Task EnterNobAsync(Player player, ICombatState combatState, PlayerChoiceContext choiceContext)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetOrCreateState(player);
        state.IsNob = true;
        await RemoveActiveGremlinPowersAsync(player);
        UpdateCombatVisuals(player);
    }

    public static async Task RevertNobAsync(Player player, ICombatState combatState, PlayerChoiceContext choiceContext)
    {
        var state = GetOrCreateState(player);
        if (!state.IsNob)
            return;

        state.IsNob = false;
        if (player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await ApplyActiveGremlinPowerAsync(player, state.Order[0], choiceContext);
            UpdateCombatVisuals(player);
        }
    }

    private static async Task RemoveActiveGremlinPowersAsync(Player player)
    {
        foreach (var power in player.Creature.Powers.OfType<GremlinActivePower>().ToList())
        {
            await PowerCmd.Remove(power);
        }
    }

    private static Task ApplyActiveGremlinPowerAsync(
        Player player,
        GremlinKind kind,
        PlayerChoiceContext choiceContext) => kind switch
    {
        GremlinKind.Mad => PowerCmd.Apply<MadGremlinPower>(choiceContext, player.Creature, 2m, player.Creature, null),
        GremlinKind.Fat => PowerCmd.Apply<FatGremlinPower>(choiceContext, player.Creature, 1m, player.Creature, null),
        GremlinKind.Shield => PowerCmd.Apply<ShieldGremlinPower>(choiceContext, player.Creature, 2m, player.Creature, null),
        GremlinKind.Sneaky => PowerCmd.Apply<SneakyGremlinPower>(choiceContext, player.Creature, 2m, player.Creature, null),
        GremlinKind.Wizard => PowerCmd.Apply<GremlinWizardPower>(choiceContext, player.Creature, 1m, player.Creature, null),
        _ => Task.CompletedTask
    };

    public static async Task TriggerBonusAsync(
        Player player,
        ICombatState combatState,
        PlayerChoiceContext choiceContext,
        GremlinKind kind,
        int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            switch (kind)
            {
                case GremlinKind.Mad:
                    await PowerCmd.Apply<FlexPotionPower>(choiceContext, player.Creature, 2m, player.Creature, null);
                    break;
                case GremlinKind.Fat:
                    foreach (var enemy in combatState.HittableEnemies.ToList())
                    {
                        if (!enemy.IsAlive || CombatManager.Instance.IsOverOrEnding)
                            continue;
                        await PowerCmd.Apply<WeakPower>(choiceContext, enemy, 1m, player.Creature, null);
                    }
                    break;
                case GremlinKind.Shield:
                    await CreatureCmd.GainBlock(player.Creature, 2m, ValueProp.Unpowered, null);
                    break;
                case GremlinKind.Sneaky:
                    await DealRandomUnpoweredDamageAsync(choiceContext, player, combatState, 2m);
                    break;
                case GremlinKind.Wizard:
                    await PowerCmd.Apply<WizPower>(choiceContext, player.Creature, 1m, player.Creature, null);
                    break;
            }
        }
    }

    public static async Task DealRandomUnpoweredDamageAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        ICombatState combatState,
        decimal baseDamage)
    {
        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count == 0 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        decimal bonus = 0m;
        foreach (var provider in player.Relics.OfType<IRandomDamageBonusProvider>())
        {
            bonus += provider.BonusRandomEnemyDamage;
        }

        var target = enemies[player.RunState.Rng.CombatTargets.NextInt(enemies.Count)];
        await CreatureCmd.Damage(
            choiceContext,
            target,
            baseDamage + bonus,
            ValueProp.Unpowered,
            dealer: player.Creature,
            cardSource: null,
            cardPlay: null);
    }

    public static bool IsEcho(CardModel card) => DownfallEcho.IsEcho(card);

    public static CardModel? MakeEcho(CardModel source) => DownfallEcho.MakeEcho(source);

    public static void UpdateCombatSprite(Player player, string texturePath)
    {
        var body = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals?.Body as Sprite2D;
        if (body is null || body.Texture?.ResourcePath == texturePath || !ResourceLoader.Exists(texturePath))
            return;

        if (ResourceLoader.Load<Texture2D>(texturePath) is { } texture)
        {
            body.Texture = texture;
        }
    }

    public static void UpdateCombatVisuals(Player player)
    {
        if (player.Character is not TheGremlins)
            return;

        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        var visuals = creatureNode?.Visuals;
        if (creatureNode is null || visuals is null)
            return;

        var state = GetOrCreateState(player);
        string activePath = state.IsNob
            ? NobSpriteTexturePath
            : SpriteTexturePathFor(state.Order[0]);
        UpdateCombatSprite(player, activePath);

        var row = visuals.GetNodeOrNull<Node2D>(StandbyRowNodeName);
        if (row is null)
        {
            var templateHealthBar = creatureNode
                .GetNodeOrNull<NCreatureStateDisplay>("%HealthBar")
                ?.GetNodeOrNull<NHealthBar>("%HealthBar");
            row = CreateStandbyRowNode(player, templateHealthBar, out var proxies);
            StandbyHealthProxies.AddOrUpdate(row, proxies);
            visuals.AddChild(row);
            // Draw behind active Gremlin Body (child 1+) while staying at ZIndex = 0 (in front of combat background!).
            visuals.MoveChild(row, 0);
            // Initialize native NHealthBars AFTER entering the SceneTree so _Ready() has wired all %Node refs!
            InitializeStandbyHealthBars(row, proxies);
        }

        var hoverLayer = creatureNode.GetNodeOrNull<Control>(StandbyHoverLayerNodeName);
        if (hoverLayer is null)
        {
            hoverLayer = CreateStandbyHoverLayer(player);
            creatureNode.AddChild(hoverLayer);
        }
        hoverLayer.Position = visuals.Position;

        if (state.IsNob)
        {
            row.Visible = false;
            hoverLayer.Visible = false;
            return;
        }

        row.Visible = true;
        hoverLayer.Visible = true;
        int maxHp = Math.Max(1, player.Creature.MaxHp);
        var livingStandby = state.Order.Skip(1).Where(k => state.Hp[(int)k] > 0).ToList();
        StandbyHealthProxies.TryGetValue(row, out var healthProxies);

        for (int i = 0; i < StandbySlotOffsets.Length; i++)
        {
            var slot = row.GetNodeOrNull<Node2D>($"Slot{i}");
            var hover = hoverLayer.GetNodeOrNull<Control>($"Hover{i}");

            if (i >= livingStandby.Count)
            {
                if (slot is not null)
                    slot.Visible = false;
                if (hover is not null)
                    hover.Visible = false;
                continue;
            }

            GremlinKind kind = livingStandby[i];
            int hp = Math.Clamp(state.Hp[(int)kind], 1, maxHp);

            if (slot is not null)
            {
                slot.Visible = true;
                if (slot.GetNodeOrNull<Sprite2D>("Sprite") is { } sprite)
                {
                    string texPath = SpriteTexturePathFor(kind);
                    if (sprite.Texture?.ResourcePath != texPath && ResourceLoader.Exists(texPath))
                    {
                        sprite.Texture = ResourceLoader.Load<Texture2D>(texPath);
                    }
                }

                if (healthProxies is not null && slot.GetNodeOrNull<NHealthBar>("HealthBar") is { } hpBar)
                {
                    var proxy = healthProxies[i];
                    proxy.SetMaxHpInternal(maxHp);
                    proxy.SetCurrentHpInternal(hp);
                    hpBar.UpdateWidthRelativeToReferenceValue(maxHp, StandbyHealthBarWidth);
                    hpBar.RefreshValues();
                }
            }

            if (hover is not null)
            {
                hover.Visible = true;
                hover.SetMeta("gremlin_kind", (int)kind);
            }
        }
    }

    private static Node2D CreateStandbyRowNode(
        Player player,
        NHealthBar? templateHealthBar,
        out Creature[] proxies)
    {
        // IMPORTANT: Keep ZIndex = 0 (never negative!) so Godot draws standby Gremlins IN FRONT of
        // the ZIndex = 0 combat room background, while MoveChild(row, 0) keeps them BEHIND active Body.
        var row = new Node2D
        {
            Name = StandbyRowNodeName,
            ZIndex = 0
        };

        int maxHp = Math.Max(1, player.Creature.MaxHp);
        proxies = new Creature[StandbySlotOffsets.Length];

        PackedScene? healthBarScene = null;
        string? scenePath = templateHealthBar?.SceneFilePath;
        if (string.IsNullOrEmpty(scenePath) && ResourceLoader.Exists("res://scenes/combat/health_bar.tscn"))
            scenePath = "res://scenes/combat/health_bar.tscn";
        if (!string.IsNullOrEmpty(scenePath) && ResourceLoader.Exists(scenePath))
            healthBarScene = ResourceLoader.Load<PackedScene>(scenePath);

        foreach (int i in StandbyDrawOrder)
        {
            var slot = new Node2D
            {
                Name = $"Slot{i}",
                Position = StandbySlotOffsets[i],
                ZIndex = 0
            };

            var sprite = new Sprite2D
            {
                Name = "Sprite",
                Position = new Vector2(0f, -86f),
                Scale = new Vector2(0.62f, 0.62f),
                Modulate = new Color(0.94f, 0.94f, 0.98f, 1f)
            };
            slot.AddChild(sprite);

            var proxy = new Creature(player, maxHp, maxHp);
            proxies[i] = proxy;

            if (healthBarScene?.Instantiate<NHealthBar>() is { } hpBar)
            {
                hpBar.Name = "HealthBar";
                hpBar.Scale = new Vector2(StandbyHealthBarScale, StandbyHealthBarScale);
                hpBar.Position = new Vector2(-StandbyHealthBarWidth * StandbyHealthBarScale * 0.5f, 2f);
                SetMouseFilterIgnoreRecursive(hpBar);
                slot.AddChild(hpBar);
            }

            row.AddChild(slot);
        }

        return row;
    }

    private static void InitializeStandbyHealthBars(Node2D row, Creature[] proxies)
    {
        for (int i = 0; i < proxies.Length; i++)
        {
            if (row.GetNodeOrNull<Node2D>($"Slot{i}")?.GetNodeOrNull<NHealthBar>("HealthBar") is not { } hpBar)
                continue;

            if (hpBar.GetNodeOrNull<Control>("%BlockContainer") is { } blockContainer)
                blockContainer.Visible = false;
            if (hpBar.GetNodeOrNull<Control>("%BlockOutline") is { } blockOutline)
                blockOutline.Visible = false;

            hpBar.HpBarContainer.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            hpBar.HpBarContainer.Position = Vector2.Zero;
            hpBar.SetCreature(proxies[i]);
        }
    }

    private static void SetMouseFilterIgnoreRecursive(Node node)
    {
        if (node is Control ctrl)
            ctrl.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren())
            SetMouseFilterIgnoreRecursive(child);
    }

    private static Control CreateStandbyHoverLayer(Player player)
    {
        var layer = new Control
        {
            Name = StandbyHoverLayerNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        foreach (int i in StandbyDrawOrder)
        {
            Vector2 offset = StandbySlotOffsets[i];
            var hover = new Control
            {
                Name = $"Hover{i}",
                MouseFilter = Control.MouseFilterEnum.Pass,
                OffsetLeft = offset.X - 42f,
                OffsetRight = offset.X + 42f,
                OffsetTop = offset.Y - 128f,
                OffsetBottom = offset.Y + 24f
            };

            hover.Connect(Control.SignalName.MouseEntered, Callable.From(() =>
            {
                if (!hover.IsVisibleInTree() || !hover.HasMeta("gremlin_kind"))
                    return;
                var kind = (GremlinKind)hover.GetMeta("gremlin_kind").AsInt32();
                NHoverTipSet.CreateAndShow(hover, BuildSingleGremlinHoverTip(player, kind), HoverTipAlignment.Right);
            }));
            hover.Connect(Control.SignalName.MouseExited, Callable.From(() => NHoverTipSet.Remove(hover)));
            hover.Connect(CanvasItem.SignalName.Hidden, Callable.From(() => NHoverTipSet.Remove(hover)));
            hover.Connect(Node.SignalName.TreeExiting, Callable.From(() => NHoverTipSet.Remove(hover)));

            layer.AddChild(hover);
        }

        return layer;
    }
}
