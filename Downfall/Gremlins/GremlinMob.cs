using System.Runtime.CompilerServices;
using Downfall.Gremlins.Cards;
using Downfall.Gremlins.Powers;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
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
    private sealed class MobState
    {
        public bool Initialized;
        public List<GremlinKind> Order = [];
        public bool IsNob;
    }

    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, MobState>> States = new();
    private static readonly ConditionalWeakTable<CardModel, object> EchoCards = new();
    private static readonly object EchoMarker = new();

    public static readonly GremlinKind[] AllKinds =
    [
        GremlinKind.Mad,
        GremlinKind.Fat,
        GremlinKind.Shield,
        GremlinKind.Sneaky,
        GremlinKind.Wizard
    ];

    public const string NobSpriteTexturePath = "res://Downfall/images/gremlins/char/nob.png";

    public static string SpriteTexturePathFor(GremlinKind kind) => kind switch
    {
        GremlinKind.Mad => "res://Downfall/images/gremlins/char/angry.png",
        GremlinKind.Fat => "res://Downfall/images/gremlins/char/fat.png",
        GremlinKind.Shield => "res://Downfall/images/gremlins/char/shield.png",
        GremlinKind.Sneaky => "res://Downfall/images/gremlins/char/sneak.png",
        GremlinKind.Wizard => "res://Downfall/images/gremlins/char/wizard.png",
        _ => "res://Downfall/images/gremlins/char/gremlins.png"
    };

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

    private static MobState GetState(ICombatState combatState, Player player)
    {
        var map = States.GetOrCreateValue(combatState);
        if (!map.TryGetValue(player, out var state))
        {
            state = new MobState();
            map[player] = state;
        }
        return state;
    }

    public static bool IsNob(ICombatState? combatState, Player? player) =>
        combatState is not null && player is not null && GetState(combatState, player).IsNob;

    public static GremlinKind GetActiveKind(ICombatState? combatState, Player? player)
    {
        if (combatState is null || player is null)
            return GremlinKind.Mad;
        var state = GetState(combatState, player);
        return state.Order.Count > 0 ? state.Order[0] : GremlinKind.Mad;
    }

    public static IReadOnlyList<GremlinKind> GetStandbyKinds(ICombatState? combatState, Player? player)
    {
        if (combatState is null || player is null)
            return [];
        var state = GetState(combatState, player);
        if (state.Order.Count <= 1)
            return [];
        return state.Order.Skip(1).ToList();
    }

    public static int GetLivingCount(ICombatState? combatState, Player? player)
    {
        if (combatState is null || player is null)
            return 5;
        var state = GetState(combatState, player);
        if (state.Initialized)
            return state.Order.Count;
        return AllKinds.Length - GetSacrificedKinds(player).Count;
    }

    private static HashSet<GremlinKind> GetSacrificedKinds(Player player)
    {
        var set = new HashSet<GremlinKind>();
        foreach (var relic in player.Relics.OfType<IGremlinSacrificeProvider>())
        {
            if (relic.SacrificedGremlin is { } kind)
                set.Add(kind);
        }
        return set;
    }

    public static async Task EnsureInitializedAsync(
        ICombatState combatState,
        Player player,
        PlayerChoiceContext choiceContext)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var state = GetState(combatState, player);
        if (state.Initialized)
            return;

        state.Initialized = true;
        var sacrificed = GetSacrificedKinds(player);
        var roster = AllKinds.Where(k => !sacrificed.Contains(k)).ToList();
        if (roster.Count == 0)
            roster.Add(GremlinKind.Mad);

        // Deterministic Fisher-Yates shuffle using combat RNG
        var rng = player.RunState.Rng.CombatCardGeneration;
        for (int i = roster.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (roster[i], roster[j]) = (roster[j], roster[i]);
        }

        state.Order = roster;
        await ApplyActiveGremlinPowerAsync(player, state.Order[0], choiceContext);
        UpdateCombatSprite(player, SpriteTexturePathFor(state.Order[0]));
    }

    public static async Task<bool> SwapNextAsync(
        Player player,
        ICombatState combatState,
        PlayerChoiceContext choiceContext)
    {
        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetState(combatState, player);
        if (state.IsNob || state.Order.Count < 2 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        return await SwapInternalAsync(player, state, state.Order[1], choiceContext);
    }

    public static async Task<bool> SwapToSpecificAsync(
        Player player,
        ICombatState combatState,
        GremlinKind target,
        PlayerChoiceContext choiceContext)
    {
        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetState(combatState, player);
        if (state.IsNob || state.Order.Count < 2 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        int idx = state.Order.IndexOf(target);
        if (idx <= 0)
            return false;

        return await SwapInternalAsync(player, state, target, choiceContext);
    }

    public static async Task<bool> SwapRandomAsync(
        Player player,
        ICombatState combatState,
        PlayerChoiceContext choiceContext)
    {
        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetState(combatState, player);
        if (state.IsNob || state.Order.Count < 2 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        int pick = 1 + player.RunState.Rng.CombatCardGeneration.NextInt(state.Order.Count - 1);
        return await SwapInternalAsync(player, state, state.Order[pick], choiceContext);
    }

    public static async Task<bool> ChooseGremlinToSwapAsync(
        Player player,
        ICombatState combatState,
        PlayerChoiceContext choiceContext)
    {
        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetState(combatState, player);
        if (state.IsNob || state.Order.Count < 2 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        if (state.Order.Count == 2)
        {
            return await SwapNextAsync(player, combatState, choiceContext);
        }

        var choices = new List<CardModel>(state.Order.Count - 1);
        for (int i = 1; i < state.Order.Count; i++)
        {
            choices.Add(CreateChoiceCard(state.Order[i], combatState, player));
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
        MobState state,
        GremlinKind next,
        PlayerChoiceContext choiceContext)
    {
        GremlinKind previous = state.Order[0];
        if (previous == next)
            return false;

        state.Order.Remove(next);
        state.Order.RemoveAt(0);
        state.Order.Insert(0, next);
        state.Order.Add(previous);

        await RemoveActiveGremlinPowersAsync(player);
        await ApplyActiveGremlinPowerAsync(player, next, choiceContext);
        UpdateCombatSprite(player, SpriteTexturePathFor(next));

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

    public static async Task EnterNobAsync(Player player, ICombatState combatState, PlayerChoiceContext choiceContext)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await EnsureInitializedAsync(combatState, player, choiceContext);
        var state = GetState(combatState, player);
        state.IsNob = true;
        await RemoveActiveGremlinPowersAsync(player);
        UpdateCombatSprite(player, NobSpriteTexturePath);
    }

    public static async Task RevertNobAsync(Player player, ICombatState combatState, PlayerChoiceContext choiceContext)
    {
        var state = GetState(combatState, player);
        if (!state.IsNob)
            return;

        state.IsNob = false;
        if (state.Order.Count > 0 && player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await ApplyActiveGremlinPowerAsync(player, state.Order[0], choiceContext);
            UpdateCombatSprite(player, SpriteTexturePathFor(state.Order[0]));
        }
    }

    private static async Task RemoveActiveGremlinPowersAsync(Player player)
    {
        foreach (var power in player.Creature.Powers.OfType<GremlinActivePower>().ToList())
        {
            await PowerCmd.Remove(power);
        }
    }

    private static Task ApplyActiveGremlinPowerAsync(Player player, GremlinKind kind, PlayerChoiceContext choiceContext) => kind switch
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

    public static bool IsEcho(CardModel card) => EchoCards.TryGetValue(card, out _);

    public static CardModel? MakeEcho(CardModel source)
    {
        if (!source.IsMutable || IsEcho(source))
            return null;

        var clone = source.CreateClone();
        clone.AddKeyword(CardKeyword.Ethereal);
        if (clone.Type != CardType.Power)
        {
            clone.AddKeyword(CardKeyword.Exhaust);
        }
        EchoCards.AddOrUpdate(clone, EchoMarker);
        return clone;
    }

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
}
