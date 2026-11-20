using Downfall.Core;
using Downfall.Hexaghost.Cards;
using Downfall.Hexaghost.Orbs;
using Downfall.Hexaghost.Powers;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Orbs;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Downfall.Hexaghost;

/// <summary>
/// Implemented by powers or relics that react whenever a Ghostflame is Ignited.
/// </summary>
public interface IGhostflameIgniteListener
{
    Task OnGhostflameIgnitedAsync(PlayerChoiceContext choiceContext, Player player, GhostflameOrb ghostflame, int ignitedCount);
}

/// <summary>
/// Implemented by powers or relics that react whenever the player Advances to the next Ghostflame.
/// </summary>
public interface IGhostflameAdvanceListener
{
    Task OnGhostflameAdvancedAsync(PlayerChoiceContext choiceContext, Player player, bool isEndTurn);
}

/// <summary>
/// Implemented by powers or relics that react whenever the player Retracts to the previous Ghostflame.
/// </summary>
public interface IGhostflameRetractListener
{
    Task OnGhostflameRetractedAsync(PlayerChoiceContext choiceContext, Player player);
}

/// <summary>
/// Implemented by powers or relics that react whenever a Ghostflame is Extinguished.
/// </summary>
public interface IGhostflameExtinguishListener
{
    Task OnGhostflameExtinguishedAsync(PlayerChoiceContext choiceContext, Player player, GhostflameOrb ghostflame);
}

/// <summary>
/// Implemented by relics/powers (e.g. Candle of Cauterizing) that add flat bonus Soulburn.
/// </summary>
public interface ISoulburnBonusProvider
{
    int GetSoulburnBonus(Player player);
    void NotifySoulburnBonusTriggered(Player player);
}

/// <summary>
/// Implemented by powers (e.g. StopFromAdvancingPower / Here and Now) that stop end-of-turn Advance.
/// </summary>
public interface IStopAdvanceProvider
{
    bool PreventsEndTurnAdvance(Player player);
}

/// <summary>
/// Implemented by relics (e.g. JarOfFuel / Olexa's Shield) that let Powers count toward Searing/Crushing Ghostflames.
/// </summary>
public interface IGhostflamePowerTriggerProvider
{
    bool AllowsPowerTrigger(Player player);
    void NotifyPowerTrigger(Player player);
}

public static class HexaMechanics
{
    public const int WheelSlotCount = 6;

    public static IReadOnlyList<GhostflameOrb> GetGhostflames(Player? player)
    {
        var orbs = player?.PlayerCombatState?.OrbQueue.Orbs;
        if (orbs is null || orbs.Count == 0)
            return [];

        return orbs.OfType<GhostflameOrb>().ToList();
    }

    public static int GetActiveSlotIndex(Player? player)
    {
        var combatState = player?.Creature.CombatState;
        if (combatState is null)
            return 0;

        var flames = GetGhostflames(player);
        if (flames.Count == 0)
            return 0;

        var state = HexaCombatState.Get(combatState);
        int idx = state.ActiveSlotIndex;
        if (idx < 0 || idx >= flames.Count)
        {
            idx = ((idx % flames.Count) + flames.Count) % flames.Count;
            state.ActiveSlotIndex = idx;
        }
        return idx;
    }

    public static GhostflameOrb? GetActiveGhostflame(Player? player)
    {
        var flames = GetGhostflames(player);
        if (flames.Count == 0)
            return null;

        return flames[GetActiveSlotIndex(player)];
    }

    public static GhostflameOrb? GetPreviousGhostflame(Player? player)
    {
        var flames = GetGhostflames(player);
        if (flames.Count == 0)
            return null;

        int prevIdx = (GetActiveSlotIndex(player) - 1 + flames.Count) % flames.Count;
        return flames[prevIdx];
    }

    public static GhostflameOrb? GetNextGhostflame(Player? player)
    {
        var flames = GetGhostflames(player);
        if (flames.Count == 0)
            return null;

        int nextIdx = (GetActiveSlotIndex(player) + 1) % flames.Count;
        return flames[nextIdx];
    }

    public static async Task ScryExhaustEtherealAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int count,
        LocString prompt)
    {
        if (count <= 0 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var drawPile = PileType.Draw.GetPile(player);
        if (drawPile.Cards.Count == 0)
            return;

        var topCards = drawPile.Cards.Take(Math.Min(count, drawPile.Cards.Count)).ToList();
        var prefs = new CardSelectorPrefs(prompt, 0, topCards.Count);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, topCards, player, prefs)).ToList();

        foreach (var card in chosen)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await CardPileCmd.Add(card, PileType.Discard);
            if (card.Keywords.Contains(CardKeyword.Ethereal)
                && card.Pile?.Type != PileType.Exhaust
                && player.Creature.IsAlive
                && !CombatManager.Instance.IsOverOrEnding)
            {
                await CardCmd.Exhaust(choiceContext, card, causedByEthereal: false);
            }
        }
    }

    public static int GetIgnitedCount(Player? player)
    {
        return GetGhostflames(player).Count(g => g.IsIgnited);
    }

    public static int GetIntensity(Player? player)
    {
        return player?.Creature.GetPower<EnhancePower>()?.Amount ?? 0;
    }

    public static int GetSoulburnBonus(Player? player, bool flashProviders = false)
    {
        if (player is null)
            return 0;

        int bonus = 0;
        foreach (var relic in player.Relics.Where(r => !r.IsMelted).OfType<ISoulburnBonusProvider>())
        {
            int b = relic.GetSoulburnBonus(player);
            if (b != 0)
            {
                bonus += b;
                if (flashProviders)
                    relic.NotifySoulburnBonusTriggered(player);
            }
        }
        foreach (var power in player.Creature.Powers.OfType<ISoulburnBonusProvider>())
        {
            int b = power.GetSoulburnBonus(player);
            if (b != 0)
            {
                bonus += b;
                if (flashProviders)
                    power.NotifySoulburnBonusTriggered(player);
            }
        }
        return bonus;
    }

    public static int GetFlameRepeatCount(Player? player, out bool affectAllEnemies)
    {
        var power = player?.Creature.GetPower<FlameAffectAllEnemiesPower>();
        if (power is not null && power.Amount > 0)
        {
            affectAllEnemies = true;
            return power.Amount;
        }
        affectAllEnemies = false;
        return 1;
    }

    public static async Task EnsureWheelInitializedAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature.CombatState is not { } combatState
            || player.PlayerCombatState is null
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var state = HexaCombatState.Get(combatState);
        if (state.WheelInitialized && GetGhostflames(player).Count > 0)
            return;

        state.WheelInitialized = true;
        state.ActiveSlotIndex = 0;

        var orbQueue = player.PlayerCombatState.OrbQueue;
        if (orbQueue.Capacity < WheelSlotCount)
        {
            await OrbCmd.AddSlots(player, WheelSlotCount - orbQueue.Capacity);
        }

        // Exact DownfallSTS 6-slot wheel order:
        // [Searing, Crushing, Bolstering, Searing, Crushing, Inferno]
        OrbModel[] initialFlames =
        [
            ModelDb.Orb<SearingGhostflameOrb>().ToMutable(),
            ModelDb.Orb<CrushingGhostflameOrb>().ToMutable(),
            ModelDb.Orb<BolsteringGhostflameOrb>().ToMutable(),
            ModelDb.Orb<SearingGhostflameOrb>().ToMutable(),
            ModelDb.Orb<CrushingGhostflameOrb>().ToMutable(),
            ModelDb.Orb<InfernoGhostflameOrb>().ToMutable()
        ];

        foreach (var orb in initialFlames)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                return;
            await OrbCmd.Channel(choiceContext, orb, player);
        }

        RefreshGhostflameVisuals(player);
    }

    public static async Task IgniteActiveAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        bool extinguishFirstIfIgnited = false)
    {
        var active = GetActiveGhostflame(player);
        if (active is null)
            return;

        await IgniteAsync(choiceContext, player, active, extinguishFirstIfIgnited);
    }

    public static async Task IgniteAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        GhostflameOrb ghostflame,
        bool extinguishFirstIfIgnited = false)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (ghostflame.IsIgnited)
        {
            if (!extinguishFirstIfIgnited)
                return;
            await ExtinguishAsync(choiceContext, player, ghostflame);
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;
        }

        ghostflame.MarkIgnited();
        int ignitedCount = GetIgnitedCount(player);
        if (player.Creature.CombatState is { } combatState)
        {
            var state = HexaCombatState.Get(combatState);
            state.IgnitionsThisTurn++;
            state.IgnitionsThisCombat++;
        }

        RefreshGhostflameVisuals(player);
        ghostflame.PlayIgniteVisual();
        DownfallSpriteAnimator.ForCreature(player.Creature)?.PlayHexaPulse(new Color(1.25f, 1.65f, 1.35f, 1f), 0.20f);

        await ghostflame.OnIgniteAsync(choiceContext, player);
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var listeners = GetListeners<IGhostflameIgniteListener>(player);
        foreach (var listener in listeners)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnGhostflameIgnitedAsync(choiceContext, player, ghostflame, ignitedCount);
        }

        RefreshGhostflameVisuals(player);
    }

    public static async Task ExtinguishActiveAsync(PlayerChoiceContext choiceContext, Player player)
    {
        var active = GetActiveGhostflame(player);
        if (active is null)
            return;

        await ExtinguishAsync(choiceContext, player, active);
    }

    public static async Task ExtinguishAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        GhostflameOrb ghostflame)
    {
        if (!ghostflame.IsIgnited || CombatManager.Instance.IsOverOrEnding)
            return;

        ghostflame.MarkExtinguished();
        RefreshGhostflameVisuals(player);
        ghostflame.PlayExtinguishVisual();

        if (!player.Creature.IsAlive)
            return;

        var listeners = GetListeners<IGhostflameExtinguishListener>(player);
        foreach (var listener in listeners)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnGhostflameExtinguishedAsync(choiceContext, player, ghostflame);
        }

        RefreshGhostflameVisuals(player);
    }

    public static async Task AdvanceAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        bool isEndTurn = false)
    {
        if (player.Creature.CombatState is not { } combatState
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var flames = GetGhostflames(player);
        if (flames.Count == 0)
            return;

        var state = HexaCombatState.Get(combatState);
        state.ActiveSlotIndex = (GetActiveSlotIndex(player) + 1) % flames.Count;
        state.AdvancesThisTurn++;
        state.AdvancesThisCombat++;
        DownfallSpriteAnimator.ForCreature(player.Creature)?.PlayHexaPulse(new Color(1.30f, 1.05f, 1.55f, 1f), 0.24f);

        var entered = flames[state.ActiveSlotIndex];
        await ExtinguishAsync(choiceContext, player, entered);

        var listeners = GetListeners<IGhostflameAdvanceListener>(player);
        foreach (var listener in listeners)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnGhostflameAdvancedAsync(choiceContext, player, isEndTurn);
        }

        RefreshGhostflameVisuals(player);
    }

    public static async Task RetractAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature.CombatState is not { } combatState
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var flames = GetGhostflames(player);
        if (flames.Count == 0)
            return;

        var state = HexaCombatState.Get(combatState);
        state.ActiveSlotIndex = (GetActiveSlotIndex(player) - 1 + flames.Count) % flames.Count;
        DownfallSpriteAnimator.ForCreature(player.Creature)?.PlayHexaPulse(new Color(1.15f, 1.15f, 1.55f, 1f), -0.24f);

        var entered = flames[state.ActiveSlotIndex];
        await ExtinguishAsync(choiceContext, player, entered);

        var listeners = GetListeners<IGhostflameRetractListener>(player);
        foreach (var listener in listeners)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnGhostflameRetractedAsync(choiceContext, player);
        }

        RefreshGhostflameVisuals(player);
    }

    public static async Task SwitchToRandomOtherGhostflameAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature.CombatState is not { } combatState
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var flames = GetGhostflames(player);
        if (flames.Count <= 1)
            return;

        int currentIdx = GetActiveSlotIndex(player);
        var otherIndices = Enumerable.Range(0, flames.Count).Where(i => i != currentIdx).ToList();
        int nextIdx = player.RunState.Rng.CombatCardSelection.NextItem(otherIndices);

        var state = HexaCombatState.Get(combatState);
        state.ActiveSlotIndex = nextIdx;
        await ExtinguishAsync(choiceContext, player, flames[nextIdx]);
        RefreshGhostflameVisuals(player);
    }

    public static void RandomizeActiveGhostflame(Player player)
    {
        if (player.Creature.CombatState is null
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var flames = GetGhostflames(player);
        if (flames.Count == 0)
            return;

        int slotIndex = GetActiveSlotIndex(player);
        var current = flames[slotIndex];

        var candidates = new List<Func<GhostflameOrb>>(3);
        if (current is not SearingGhostflameOrb)
            candidates.Add(() => (GhostflameOrb)ModelDb.Orb<SearingGhostflameOrb>().ToMutable());
        if (current is not CrushingGhostflameOrb)
            candidates.Add(() => (GhostflameOrb)ModelDb.Orb<CrushingGhostflameOrb>().ToMutable());
        if (current is not BolsteringGhostflameOrb)
            candidates.Add(() => (GhostflameOrb)ModelDb.Orb<BolsteringGhostflameOrb>().ToMutable());

        if (candidates.Count == 0)
            return;

        var factory = player.RunState.Rng.CombatCardSelection.NextItem(candidates)!;
        ReplaceGhostflame(player, slotIndex, factory());
    }

    public static bool AllowsPowerForFlameProgression(Player? player, bool flashProviders = false)
    {
        if (player is null)
            return false;

        bool allowed = false;
        foreach (var relic in player.Relics)
        {
            if (!relic.IsMelted && relic is IGhostflamePowerTriggerProvider provider && provider.AllowsPowerTrigger(player))
            {
                allowed = true;
                if (flashProviders)
                    provider.NotifyPowerTrigger(player);
            }
        }
        return allowed;
    }

    public static void ReplaceGhostflame(Player player, int slotIndex, GhostflameOrb newOrb)
    {
        if (player.PlayerCombatState is null || CombatManager.Instance.IsOverOrEnding)
            return;

        var orbQueue = player.PlayerCombatState.OrbQueue;
        var flames = GetGhostflames(player);
        if (slotIndex < 0 || slotIndex >= flames.Count)
            return;

        var oldOrb = flames[slotIndex];
        int queueIndex = -1;
        for (int i = 0; i < orbQueue.Orbs.Count; i++)
        {
            if (orbQueue.Orbs[i] == oldOrb)
            {
                queueIndex = i;
                break;
            }
        }

        if (queueIndex < 0)
            return;

        newOrb.AssertMutable();
        newOrb.Owner = player;
        orbQueue.Remove(oldOrb);
        oldOrb.RemoveInternal();
        orbQueue.Insert(queueIndex, newOrb);

        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.ReplaceOrb(oldOrb, newOrb);
        RefreshGhostflameVisuals(player);
    }

    public static async Task ApplySoulburnAsync(
        PlayerChoiceContext choiceContext,
        Creature target,
        int baseAmount,
        Creature applier,
        CardModel? cardSource = null)
    {
        if (!target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int bonus = applier.Player is { } player ? GetSoulburnBonus(player, flashProviders: true) : 0;
        int total = baseAmount + bonus;
        if (total <= 0)
            return;

        await PowerCmd.Apply<SoulburnPower>(choiceContext, target, total, applier, cardSource);

        if (target.CombatState is { } combatState)
            HexaCombatState.Get(combatState).SoulburnAppliedThisCombat += total;
    }

    public static async Task ChooseMoveGhostflameAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature.CombatState is not { } combatState
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        CardModel[] choices =
        [
            combatState.CreateCard<HexaRetractChoice>(player),
            combatState.CreateCard<HexaStayChoice>(player),
            combatState.CreateCard<HexaAdvanceChoice>(player)
        ];

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, player, canSkip: false);
        if (chosen is HexaRetractChoice)
            await RetractAsync(choiceContext, player);
        else if (chosen is HexaAdvanceChoice)
            await AdvanceAsync(choiceContext, player, isEndTurn: false);
    }

    public static void RefreshGhostflameVisuals(Player? player)
    {
        if (player is null)
            return;

        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.UpdateVisuals(OrbEvokeType.None);
    }

    public static bool PreventsEndTurnAdvance(Player player)
    {
        return player.Creature.Powers.OfType<IStopAdvanceProvider>().Any(p => p.PreventsEndTurnAdvance(player));
    }

    private static List<T> GetListeners<T>(Player player) where T : class
    {
        var result = new List<T>();
        foreach (var relic in player.Relics)
        {
            if (!relic.IsMelted && relic is T listener)
                result.Add(listener);
        }
        foreach (var power in player.Creature.Powers)
        {
            if (power is T listener)
                result.Add(listener);
        }
        if (player.PlayerCombatState is { } pcs)
        {
            foreach (var card in pcs.DiscardPile.Cards)
            {
                if (card is T listener)
                    result.Add(listener);
            }
        }
        return result;
    }
}
