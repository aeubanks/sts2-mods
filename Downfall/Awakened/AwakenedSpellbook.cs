using System.Runtime.CompilerServices;
using Downfall.Awakened.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
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
        state.Queue.Add(canonicalSpell);
    }

    public static void TryCheckAwakening(ICombatState combatState, Player owner, bool force = false)
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
    }

    public static async Task<CardModel?> ConjureAsync(
        Player owner,
        ICombatState combatState,
        PileType destination = PileType.Hand,
        CardPilePosition position = CardPilePosition.Top)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return null;

        TryCheckAwakening(combatState, owner);
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
}
