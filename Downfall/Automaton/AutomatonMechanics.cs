using Downfall.Automaton.Cards;
using Downfall.Automaton.Orbs;
using Downfall.Core;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Orbs;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace Downfall.Automaton;

/// <summary>
/// Implemented by powers/relics that capture extra copy counts right before a non-clone card is Encoded
/// (e.g. CloningPower / Merge, CableSpool).
/// </summary>
public interface IEncodeCopyProvider
{
    Task<int> ConsumeExtraEncodeCopiesAsync(PlayerChoiceContext choiceContext, Player player, BronzeCard card, bool isFromCardPlay);
}

/// <summary>
/// Implemented by powers that modify a card right as it enters the Function sequence
/// (e.g. OptimizePower upgrading it, RemoveNextErrorPower stripping Compile Error).
/// </summary>
public interface IOnBeforeEncodeListener
{
    Task OnBeforeEncodeAsync(PlayerChoiceContext choiceContext, Player player, BronzeCard card);
}

/// <summary>
/// Implemented by powers that inspect/modify encoded cards right before Compilation
/// (e.g. CleanCodePower stripping Compile Errors for this Function).
/// </summary>
public interface IPreCompileListener
{
    Task OnPreCompileAsync(PlayerChoiceContext choiceContext, Player player, IReadOnlyList<BronzeCard> encodedCards);
}

/// <summary>
/// Implemented by powers/relics that react when a FunctionCard finishes compiling
/// (e.g. BronzeCore, PlatinumCore, Timepiece, FreeFunctionPower, HardenedFormPower).
/// </summary>
public interface IOnFunctionCompiledListener
{
    Task OnFunctionCompiledAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function);
}

/// <summary>
/// Implemented by powers/relics that react after the compiled FunctionCard is added to Hand
/// (e.g. DefaultPower / Class Default re-encoding Slot 0).
/// </summary>
public interface IAfterFunctionOutputListener
{
    Task OnAfterFunctionOutputAsync(PlayerChoiceContext choiceContext, Player player, BronzeCard? firstSlotCard);
}

/// <summary>
/// Implemented by cards that update when any Function is compiled (e.g. ForceShield cost discount).
/// </summary>
public interface IFunctionCompiledCardListener
{
    void OnFunctionCompiled(Player player, int totalFunctionsCompiled);
}

public static class AutomatonMechanics
{
    public const int FunctionCapacity = 3;

    public static IReadOnlyList<EncodedCardOrb> GetEncodedOrbs(Player? player)
    {
        var orbs = player?.PlayerCombatState?.OrbQueue.Orbs;
        if (orbs is null || orbs.Count == 0)
            return [];

        return orbs.OfType<EncodedCardOrb>().ToList();
    }

    public static IReadOnlyList<BronzeCard> GetEncodedCards(Player? player)
    {
        return GetEncodedOrbs(player)
            .Select(o => o.EncodedCard)
            .Where(c => c is not null)
            .Cast<BronzeCard>()
            .ToList();
    }

    public static bool IsSequenceEmpty(Player? player) => GetEncodedOrbs(player).Count == 0;

    public static int GetFunctionsCompiledThisCombat(Player? player)
    {
        return player?.Creature.CombatState is { } cs
            ? AutomatonCombatState.Get(cs).FunctionsCompiledThisCombat
            : 0;
    }

    public static void ReindexEncodedOrbs(Player player)
    {
        var orbs = GetEncodedOrbs(player);
        for (int i = 0; i < orbs.Count; i++)
        {
            orbs[i].SequenceSlotIndex = i;
        }
        RefreshOrbVisuals(player);
    }

    public static void RefreshOrbVisuals(Player? player)
    {
        if (player is null)
            return;

        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.UpdateVisuals(OrbEvokeType.None);
    }

    public static Task AddGeneratedCardToHandOrDiscardAsync(CardModel card, Player player)
    {
        var dest = PileType.Hand.GetPile(player).Cards.Count < CardPile.MaxCardsInHand
            ? PileType.Hand
            : PileType.Discard;
        return CardPileCmd.AddGeneratedCardToCombat(card, dest, player);
    }

    /// <summary>
    /// Insert: shuffles <paramref name="count"/> newly generated cards of type <typeparamref name="TCard"/>
    /// into the player's Draw Pile.
    /// </summary>
    public static async Task InsertCardIntoDrawPileAsync<TCard>(Player player, int count = 1)
        where TCard : CardModel
    {
        if (player.Creature.CombatState is not { } combatState
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var card = combatState.CreateCard<TCard>(player);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, player, CardPilePosition.Random);
        }
    }

    /// <summary>
    /// Scry <paramref name="count"/>: inspect top cards of Draw Pile and optionally discard any chosen cards.
    /// Pass <paramref name="discardSelected"/> = false when the caller handles each chosen card (e.g. Assembly).
    /// </summary>
    public static async Task<IReadOnlyList<CardModel>> ScryAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int count,
        LocString prompt,
        bool discardSelected = true)
    {
        if (count <= 0 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return [];

        var drawPile = PileType.Draw.GetPile(player);
        if (drawPile.Cards.Count == 0)
            return [];

        var topCards = drawPile.Cards.Take(Math.Min(count, drawPile.Cards.Count)).ToList();
        var prefs = new CardSelectorPrefs(prompt, 0, topCards.Count);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, topCards, player, prefs)).ToList();

        if (discardSelected)
        {
            foreach (var card in chosen)
            {
                if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    break;
                await CardPileCmd.Add(card, PileType.Discard);
            }
        }

        return chosen;
    }

    /// <summary>
    /// Creates a random non-Basic, non-Token Encodable Automaton card for GetLatest / Library / SpaghettiCode / BuildAFunctionPotion.
    /// </summary>
    public static BronzeCard CreateRandomEncodableCard(Player player)
    {
        var combatState = player.Creature.CombatState!;
        var candidates = GetRandomEncodableCandidates(player);
        CardModel canonical = candidates.Count > 0
            ? player.RunState.Rng.CombatCardGeneration.NextItem(candidates)!
            : ModelDb.Card<Replicate>();
        return (BronzeCard)combatState.CreateCard(canonical, player);
    }

    public static List<BronzeCard> CreateDistinctRandomEncodableChoices(Player player, int count = 3)
    {
        var combatState = player.Creature.CombatState!;
        var pool = GetRandomEncodableCandidates(player);
        var rng = player.RunState.Rng.CombatCardGeneration;
        var result = new List<BronzeCard>(count);

        int picks = Math.Min(count, pool.Count);
        for (int i = 0; i < picks; i++)
        {
            int idx = rng.NextInt(pool.Count);
            var canonical = pool[idx];
            pool.RemoveAt(idx);
            result.Add((BronzeCard)combatState.CreateCard(canonical, player));
        }

        while (result.Count < count)
        {
            result.Add((BronzeCard)combatState.CreateCard<Replicate>(player));
        }

        return result;
    }

    /// <summary>
    /// Shared helper for SpaghettiCode and BuildAFunctionPotion: repeatedly choose 1 of 3 random
    /// Encodable cards to Encode until a Function compiles (or safety cap is reached).
    /// Returns true if a Function was compiled during the loop.
    /// </summary>
    public static async Task<bool> BuildFunctionFromChoicesAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature.CombatState is not { } combatState
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return false;
        }

        var state = AutomatonCombatState.Get(combatState);
        int startCompiled = state.FunctionsCompiledThisCombat;

        for (int safety = 0; safety < FunctionCapacity * 2; safety++)
        {
            if (!player.Creature.IsAlive
                || CombatManager.Instance.IsOverOrEnding
                || state.FunctionsCompiledThisCombat > startCompiled)
            {
                break;
            }

            var choices = CreateDistinctRandomEncodableChoices(player, 3)
                .Cast<CardModel>()
                .ToList();
            var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, player, canSkip: false);
            if (chosen is not BronzeCard bronze || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await EncodeCardAsync(choiceContext, player, bronze);
        }

        return state.FunctionsCompiledThisCombat > startCompiled;
    }

    private static List<CardModel> GetRandomEncodableCandidates(Player player)
    {
        return ModelDb.CardPool<AutomatonCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => c is BronzeCard { Encodes: true }
                && c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Removes a specific EncodedCardOrb from the OrbQueue cleanly without triggering Evoke effects,
    /// then re-indexes remaining encoded orbs.
    /// </summary>
    public static bool RemoveEncodedOrb(Player player, EncodedCardOrb orb)
    {
        if (player.PlayerCombatState is not { } pcs)
            return false;

        if (!pcs.OrbQueue.Remove(orb))
            return false;

        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.EvokeOrbAnim(orb);
        orb.RemoveInternal();
        ReindexEncodedOrbs(player);
        return true;
    }

    /// <summary>
    /// Encodes a BronzeCard into The Automaton's Function sequence.
    /// Follows exact DownfallSTS semantics:
    ///   1. Detach from current combat pile via RemoveFromCurrentPile() (never RemoveFromCombat).
    ///   2. On !isClone: snapshot cloneTemplate BEFORE Optimize/RemoveNextError/OnEncodedAsync mutate card,
    ///      and capture & consume CloningPower / CableSpool extraCopies.
    ///   3. Run IOnBeforeEncodeListener -> channel EncodedCardOrb -> run OnEncodedAsync.
    ///   4. If sequence count >= FunctionCapacity (3), CompileFunctionAsync BEFORE encoding extraCopies.
    ///   5. Encode any captured extraCopies from cloneTemplate with isClone: true.
    /// </summary>
    public static async Task EncodeCardAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        BronzeCard card,
        bool isFromCardPlay = false,
        bool isClone = false)
    {
        if (player.PlayerCombatState is not { } pcs
            || player.Creature.CombatState is null
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        // Duplicate FullRelease guard: redirect a 2nd FullRelease to Discard instead of encoding.
        if (card is FullRelease
            && GetEncodedCards(player).Any(c => c is FullRelease))
        {
            if (card.Pile?.Type != PileType.Discard)
                await CardPileCmd.Add(card, PileType.Discard);
            return;
        }

        // Detach card cleanly without unregistering it from CombatState (preserves BitShift/ByteShift/Decompile!).
        if (card.Pile is { IsCombatPile: true } oldPile)
        {
            if (oldPile.Type == PileType.Hand)
                NCombatRoom.Instance?.Ui.Hand?.Remove(card);
            card.RemoveFromCurrentPile();
        }

        // Capture extra copy count & clean template BEFORE Optimize/RemoveNextError/OnEncodedAsync mutate card.
        int extraCopies = 0;
        BronzeCard? cloneTemplate = null;
        if (!isClone)
        {
            foreach (var provider in GetListeners<IEncodeCopyProvider>(player))
            {
                if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    return;
                extraCopies += Math.Max(0, await provider.ConsumeExtraEncodeCopiesAsync(choiceContext, player, card, isFromCardPlay));
            }
            if (extraCopies > 0)
                cloneTemplate = (BronzeCard)card.CreateClone();
        }

        // If sequence is already at capacity (e.g. re-entrant encode), compile first so Channel never evicts an EncodedCardOrb.
        if (GetEncodedOrbs(player).Count >= FunctionCapacity)
        {
            await CompileFunctionAsync(choiceContext, player);
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;
        }

        // Ensure OrbQueue can hold the sequence (and evict oldest non-EncodedCardOrb if full via crossover).
        if (pcs.OrbQueue.Capacity < FunctionCapacity)
            await OrbCmd.AddSlots(player, FunctionCapacity - pcs.OrbQueue.Capacity);

        if (pcs.OrbQueue.Orbs.Count >= pcs.OrbQueue.Capacity)
        {
            var nonEncoded = pcs.OrbQueue.Orbs.FirstOrDefault(o => o is not EncodedCardOrb);
            if (nonEncoded is not null)
            {
                await EvokeNonEncodedOrbAsync(choiceContext, player, nonEncoded);
                if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    return;
            }
        }

        // Run pre-encode hooks (OptimizePower, RemoveNextErrorPower).
        foreach (var listener in GetListeners<IOnBeforeEncodeListener>(player))
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;
            await listener.OnBeforeEncodeAsync(choiceContext, player, card);
        }

        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int slotIndex = GetEncodedOrbs(player).Count;
        var orb = (EncodedCardOrb)ModelDb.Orb<EncodedCardOrb>().ToMutable();
        orb.InitializeEncodedCard(card, slotIndex);
        await OrbCmd.Channel(choiceContext, orb, player);
        RefreshOrbVisuals(player);
        orb.PlayPulseVisual();

        await card.OnEncodedAsync(choiceContext, player, slotIndex);
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (GetEncodedOrbs(player).Count >= FunctionCapacity)
        {
            await CompileFunctionAsync(choiceContext, player);
        }

        if (extraCopies > 0 && cloneTemplate is not null)
        {
            for (int i = 0; i < extraCopies; i++)
            {
                if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    break;
                var clone = (BronzeCard)cloneTemplate.CreateClone();
                await EncodeCardAsync(choiceContext, player, clone, isFromCardPlay: false, isClone: true);
            }
        }
    }

    /// <summary>
    /// Compiles all currently Encoded cards (1..3 — supports Batch early compile!) into a FunctionCard,
    /// triggers all Compile effects & listeners, and adds the FunctionCard to Hand.
    /// </summary>
    public static async Task<FunctionCard?> CompileFunctionAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.PlayerCombatState is not { } pcs
            || player.Creature.CombatState is not { } combatState
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return null;
        }

        var encodedOrbs = GetEncodedOrbs(player);
        if (encodedOrbs.Count == 0)
            return null;

        foreach (var encodedOrb in encodedOrbs)
            encodedOrb.PlayPulseVisual();
        DownfallSpriteAnimator.ForCreature(player.Creature)?.PlayCast();
        await Cmd.CustomScaledWait(0.08f, 0.16f);

        var encodedCards = new List<BronzeCard>(encodedOrbs.Count);
        for (int i = 0; i < encodedOrbs.Count; i++)
        {
            var orb = encodedOrbs[i];
            if (orb.EncodedCard is { } c)
            {
                c.SequenceSlotIndex = i;
                encodedCards.Add(c);
            }
            if (pcs.OrbQueue.Remove(orb))
            {
                NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.EvokeOrbAnim(orb);
                orb.RemoveInternal();
            }
        }
        RefreshOrbVisuals(player);

        if (encodedCards.Count == 0)
            return null;

        var firstSlotTemplate = (BronzeCard)encodedCards[0].CreateClone();

        // 1. Pre-compile listeners (CleanCodePower strips Compile Errors for this Function).
        foreach (var listener in GetListeners<IPreCompileListener>(player))
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return null;
            await listener.OnPreCompileAsync(choiceContext, player, encodedCards);
        }

        // 2. Per-card OnCompilePreEmbed (e.g. ChosenStrike permanent damage bonus before cloning into Function).
        foreach (var c in encodedCards)
        {
            if (c.CompileEnabled)
                c.OnCompilePreEmbed(player, encodedCards);
        }

        // 3. Create FunctionCard and embed clones of the encoded cards.
        var function = (FunctionCard)combatState.CreateCard<FunctionCard>(player);
        function.InitializeFromEncodedCards(encodedCards);

        // 4. Per-card OnCompileAsync (FullRelease, NullPointer, Terminator, Goto, Backtrace, Errors, etc.).
        foreach (var c in encodedCards)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return function;
            if (c.CompileEnabled)
                await c.OnCompileAsync(choiceContext, player, function);
        }

        // 5. Function-compiled listeners (Timepiece, FreeFunctionPower, PlatinumCore, BronzeCore, HardenedFormPower).
        foreach (var listener in GetListeners<IOnFunctionCompiledListener>(player))
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return function;
            await listener.OnFunctionCompiledAsync(choiceContext, player, function);
        }

        // 6. Track compiled count & notify ForceShield cards across all combat piles.
        var state = AutomatonCombatState.Get(combatState);
        state.FunctionsCompiledThisCombat++;
        int totalCompiled = state.FunctionsCompiledThisCombat;
        NotifyCompiledCardListeners(player, totalCompiled);

        // 7. Add FunctionCard (+ any extra copies queued by BuildAFunctionPotion) to Hand.
        int extraCopies = state.ExtraFunctionCopiesQueued;
        state.ExtraFunctionCopiesQueued = 0;

        await AddGeneratedCardToHandOrDiscardAsync(function, player);
        for (int i = 0; i < extraCopies; i++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            var dup = (FunctionCard)function.CreateClone();
            await AddGeneratedCardToHandOrDiscardAsync(dup, player);
        }

        // 8. Post-output listeners (DefaultPower / Class Default re-encoding Slot 0).
        foreach (var listener in GetListeners<IAfterFunctionOutputListener>(player))
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnAfterFunctionOutputAsync(choiceContext, player, firstSlotTemplate);
        }

        return function;
    }

    private static void NotifyCompiledCardListeners(Player player, int totalCompiled)
    {
        if (player.PlayerCombatState is not { } pcs)
            return;

        foreach (var pile in new[] { pcs.Hand, pcs.DrawPile, pcs.DiscardPile, pcs.ExhaustPile, pcs.PlayPile })
        {
            foreach (var card in pile.Cards)
            {
                if (card is IFunctionCompiledCardListener listener)
                    listener.OnFunctionCompiled(player, totalCompiled);
            }
        }
    }

    private static async Task EvokeNonEncodedOrbAsync(PlayerChoiceContext choiceContext, Player player, OrbModel orb)
    {
        if (player.PlayerCombatState is not { } pcs || !pcs.OrbQueue.Remove(orb))
            return;

        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.EvokeOrbAnim(orb);
        choiceContext.PushModel(orb);
        try
        {
            await orb.Evoke(choiceContext);
        }
        finally
        {
            choiceContext.PopModel(orb);
            orb.RemoveInternal();
        }
    }

    private static List<T> GetListeners<T>(Player player) where T : class
    {
        var result = new List<T>();
        foreach (var power in player.Creature.Powers)
        {
            if (power is T listener)
                result.Add(listener);
        }
        foreach (var relic in player.Relics)
        {
            if (!relic.IsMelted && relic is T listener)
                result.Add(listener);
        }
        return result;
    }
}
