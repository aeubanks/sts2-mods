using System.Runtime.CompilerServices;
using Downfall.Awakened.Cards;
using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Awakened;

internal static class AwakenedSpellbook
{
    public const int PowersToAwaken = 7;

    private sealed class CombatSpellbookState
    {
        public List<CardModel> Queue { get; } = [];
        public List<CardModel> ExtraSpells { get; } = [];
        public bool IsAwakened { get; set; }
        public int ConjuresThisCombat { get; set; }
    }

    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Player, CombatSpellbookState>> States = new();

    private static CombatSpellbookState GetState(ICombatState combatState, Player owner)
    {
        var byPlayer = States.GetOrCreateValue(combatState);
        if (!byPlayer.TryGetValue(owner, out var state))
        {
            state = new CombatSpellbookState();
            byPlayer[owner] = state;
        }
        return state;
    }

    private static IReadOnlyList<CardModel> DefaultSpells =>
    [
        ModelDb.Card<BurningStudy>(),
        ModelDb.Card<Cryostasis>(),
        ModelDb.Card<Darkleech>(),
        ModelDb.Card<Thunderbolt>()
    ];

    private static void RefillQueue(CombatSpellbookState state, Player owner)
    {
        var pool = new List<CardModel>(DefaultSpells);
        pool.AddRange(state.ExtraSpells);

        var rng = owner.RunState.Rng.CombatCardGeneration;
        while (pool.Count > 0)
        {
            int idx = rng.NextInt(pool.Count);
            state.Queue.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
    }

    public static bool IsAwakened(ICombatState? combatState, Player? owner) =>
        combatState is not null && owner is not null && GetState(combatState, owner).IsAwakened;

    public static int GetConjuresThisCombat(ICombatState? combatState, Player? owner) =>
        combatState is not null && owner is not null ? GetState(combatState, owner).ConjuresThisCombat : 0;

    public static int GetPowersPlayedThisCombat(ICombatState? combatState, Player? owner)
    {
        if (combatState is null || owner is null)
            return 0;

        return CombatManager.Instance.History.CardPlaysStarted.Count(e =>
            e.CardPlay.Card.Owner == owner && e.CardPlay.Card.Type == CardType.Power);
    }

    public static void AddSpellToSpellbook(ICombatState combatState, Player owner, CardModel canonicalSpell)
    {
        var state = GetState(combatState, owner);
        if (state.Queue.Count == 0)
        {
            RefillQueue(state, owner);
        }
        state.ExtraSpells.Add(canonicalSpell);
        int insertIndex = owner.RunState.Rng.CombatCardGeneration.NextInt(state.Queue.Count + 1);
        state.Queue.Insert(insertIndex, canonicalSpell);
    }

    public static void RefreshSpells(ICombatState combatState, Player owner)
    {
        var state = GetState(combatState, owner);
        state.Queue.Clear();
        RefillQueue(state, owner);
    }

    public static int GetRemainingQueueCount(ICombatState? combatState, Player? owner) =>
        combatState is not null && owner is not null ? GetState(combatState, owner).Queue.Count : 0;

    public static IReadOnlyList<CardModel> GetRemainingQueue(ICombatState? combatState, Player? owner) =>
        combatState is not null && owner is not null ? GetState(combatState, owner).Queue : [];

    public static async Task TryCheckAwakeningAsync(
        ICombatState combatState,
        Player owner,
        PlayerChoiceContext choiceContext,
        bool force = false)
    {
        var state = GetState(combatState, owner);
        if (state.IsAwakened)
            return;

        if (!force && GetPowersPlayedThisCombat(combatState, owner) < PowersToAwaken)
            return;

        state.IsAwakened = true;
        if (owner.PlayerCombatState is { } pcs)
        {
            foreach (var spell in pcs.AllCards.OfType<AwakenedSpellCard>())
            {
                if (spell.IsUpgradable)
                {
                    CardCmd.Upgrade(spell);
                }
            }
        }

        foreach (var listener in owner.Creature.Powers.OfType<IAwakenListener>().ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding || !owner.Creature.IsAlive)
                break;
            await listener.OnAwakenedAsync(choiceContext);
        }

        foreach (var listener in owner.Relics.OfType<IAwakenListener>().ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding || !owner.Creature.IsAlive)
                break;
            await listener.OnAwakenedAsync(choiceContext);
        }
    }

    public static async Task<CardModel?> ConjureAsync(
        Player owner,
        ICombatState combatState,
        PileType destination = PileType.Hand,
        CardPilePosition position = CardPilePosition.Top)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return null;

        if (owner.Creature.GetPower<IntensifyDebuffPower>() is { } burnout)
        {
            burnout.TriggerFlash();
            return null;
        }

        await TryCheckAwakeningAsync(combatState, owner, new ThrowingPlayerChoiceContext());
        var state = GetState(combatState, owner);
        if (state.Queue.Count == 0)
        {
            RefillQueue(state, owner);
        }

        var canonical = state.Queue[0];
        state.Queue.RemoveAt(0);
        state.ConjuresThisCombat++;

        var card = combatState.CreateCard(canonical, owner);
        if (state.IsAwakened && card.IsUpgradable)
        {
            CardCmd.Upgrade(card);
        }

        await CardPileCmd.AddGeneratedCardToCombat(card, destination, owner, position);

        if (state.Queue.Count == 0)
        {
            RefillQueue(state, owner);
        }

        return card;
    }

    public static async Task<CardModel?> ChooseSpellToConjureAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        ICombatState combatState,
        LocString prompt,
        PileType destination = PileType.Hand,
        CardPilePosition position = CardPilePosition.Top)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return null;

        if (owner.Creature.GetPower<IntensifyDebuffPower>() is { } burnout)
        {
            burnout.TriggerFlash();
            return null;
        }

        await TryCheckAwakeningAsync(combatState, owner, choiceContext);
        var state = GetState(combatState, owner);
        if (state.Queue.Count == 0)
        {
            RefillQueue(state, owner);
        }

        var choices = new List<CardModel>();
        var seen = new HashSet<CardModel>();
        foreach (var canonical in state.Queue)
        {
            if (!seen.Add(canonical))
                continue;

            var preview = combatState.CreateCard(canonical, owner);
            if (state.IsAwakened && preview.IsUpgradable)
            {
                CardCmd.Upgrade(preview);
            }
            choices.Add(preview);
        }

        var prefs = new CardSelectorPrefs(prompt, 1);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, choices, owner, prefs)).FirstOrDefault();
        if (chosen is null)
            return null;

        state.Queue.Remove(chosen.CanonicalInstance);
        state.ConjuresThisCombat++;

        await CardPileCmd.AddGeneratedCardToCombat(chosen, destination, owner, position);

        if (state.Queue.Count == 0)
        {
            RefillQueue(state, owner);
        }

        return chosen;
    }

    public static async Task<CardModel?> ChooseSpellToInscribeAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        ICombatState combatState,
        LocString prompt,
        int copies)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding || copies <= 0)
            return null;

        await TryCheckAwakeningAsync(combatState, owner, choiceContext);
        var state = GetState(combatState, owner);

        var pool = new List<CardModel>(DefaultSpells);
        var esp = ModelDb.Card<EspSpell>();
        if (state.ExtraSpells.Contains(esp))
        {
            pool.Add(esp);
        }

        var choices = new List<CardModel>(pool.Count);
        foreach (var canonical in pool)
        {
            var preview = combatState.CreateCard(canonical, owner);
            if (state.IsAwakened && preview.IsUpgradable)
            {
                CardCmd.Upgrade(preview);
            }
            choices.Add(preview);
        }

        var prefs = new CardSelectorPrefs(prompt, 1);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, choices, owner, prefs)).FirstOrDefault();
        if (chosen is null)
            return null;

        for (int i = 0; i < copies; i++)
        {
            AddSpellToSpellbook(combatState, owner, chosen.CanonicalInstance);
        }

        return chosen.CanonicalInstance;
    }

    public static async Task NotifyEnergyDrainedAsync(
        Player player,
        ICombatState? combatState,
        PlayerChoiceContext choiceContext,
        int energyLost)
    {
        if (combatState is null || energyLost <= 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        foreach (var listener in player.Creature.Powers.OfType<IEnergyDrainListener>().ToList())
        {
            await listener.OnEnergyDrainedAsync(choiceContext, energyLost);
        }

        foreach (var listener in player.Relics.OfType<IEnergyDrainListener>().ToList())
        {
            await listener.OnEnergyDrainedAsync(choiceContext, energyLost);
        }

        foreach (var enemy in combatState.HittableEnemies.ToList())
        {
            foreach (var listener in enemy.Powers.OfType<IEnergyDrainListener>().ToList())
            {
                await listener.OnEnergyDrainedAsync(choiceContext, energyLost);
            }
        }
    }
}
