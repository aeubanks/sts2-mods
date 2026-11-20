using Downfall.Collector.Cards;
using Downfall.Collector.Cards.Collectibles;
using Downfall.Collector.Powers;
using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Collector;

/// <summary>
/// Implemented by powers that react when a card is Pyred (e.g. ShootingStarPower).
/// </summary>
public interface IOnPyreListener
{
    Task OnPyreAsync(PlayerChoiceContext choiceContext, Player player, CardModel pyredCard);
}

/// <summary>
/// Implemented by JadeRing relic to add bonus damage to Doom detonations.
/// </summary>
public interface IDoomBonusProvider
{
    int GetDoomDamageBonus();
}

/// <summary>
/// Implemented by HolidayCoal relic to generate LuckyWick when drawing from an empty Combat Collection.
/// </summary>
public interface IEmptyCollectionDrawProvider
{
    CardModel? TryCreateFallbackCollectible(ICombatState combatState, Player player);
}

/// <summary>
/// Implemented by powers that grant extra Collected card draws at the start of each turn.
/// </summary>
public interface IExtraCollectionDrawProvider
{
    int ExtraCollectionDraws { get; }
}

public static class CollectorMechanics
{
    private static readonly Type[] NormalCollectibles =
    [
        typeof(CultistCard),
        typeof(JawWormCard),
        typeof(AcidSlimeCard),
        typeof(SpikeSlimeCard),
        typeof(FungiBeastCard),
        typeof(ByrdCard),
        typeof(ChosenCard),
        typeof(SneckoCard),
        typeof(ShelledParasiteCard)
    ];

    private static readonly Type[] EliteCollectibles =
    [
        typeof(LagavulinCard),
        typeof(SentryCard),
        typeof(BronzeOrbCard),
        typeof(NemesisCard)
    ];

    private static readonly Type[] BossCollectibles =
    [
        typeof(OuroborosCard),
        typeof(TorchHeadCard),
        typeof(NemesisCard),
        typeof(BronzeOrbCard)
    ];

    public static IReadOnlyList<Type> AllPoolCollectibleTypes { get; } =
    [
        .. NormalCollectibles,
        .. EliteCollectibles,
        typeof(OuroborosCard),
        typeof(TorchHeadCard)
    ];

    public static bool IsAfflicted(Creature creature) =>
        creature.IsAlive
        && creature.HasPower<WeakPower>()
        && creature.HasPower<VulnerablePower>();

    public static int GetDoomDamageBonus(ICombatState? combatState)
    {
        if (combatState is null)
            return 0;

        int bonus = 0;
        foreach (var player in combatState.Players)
        {
            foreach (var relic in player.Relics)
            {
                if (!relic.IsMelted && relic is IDoomBonusProvider provider)
                    bonus += provider.GetDoomDamageBonus();
            }
        }
        return bonus;
    }

    public static async Task ApplyDoomAsync(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (amount <= 0m || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<CollectorDoomPower>(choiceContext, target, amount, applier, cardSource);
    }

    /// <summary>
    /// Live Reserves count for a player, clamped by current Energy (handles mid-turn Energy loss from Void/etc.).
    /// </summary>
    public static int GetReserves(Player player)
    {
        if (player.PlayerCombatState is not { } pcs)
            return 0;

        int powerAmt = player.Creature.GetPowerAmount<ReservesPower>();
        return Math.Max(0, Math.Min(powerAmt, pcs.Energy));
    }

    /// <summary>
    /// Grants Reserves cleanly: adds ReservesPower AND adds matching Energy to pcs.Energy
    /// so regular Energy is spent first and Reserves seamlessly cover any shortfall across turns.
    /// </summary>
    public static async Task GainReservesAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int amount,
        CardModel? cardSource = null)
    {
        if (amount <= 0
            || player.PlayerCombatState is null
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await PowerCmd.Apply<ReservesPower>(choiceContext, player.Creature, amount, player.Creature, cardSource);
        if (player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PlayerCmd.GainEnergy(amount, player);
    }

    public static async Task GainTempHpAsync(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        CardModel? cardSource = null)
    {
        if (amount <= 0m || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<DownfallTempHpPower>(choiceContext, target, amount, target, cardSource);
    }

    public static Task AddGeneratedCardToHandOrDiscardAsync(CardModel card, Player player)
    {
        var dest = PileType.Hand.GetPile(player).Cards.Count < CardPile.MaxCardsInHand
            ? PileType.Hand
            : PileType.Discard;
        return CardPileCmd.AddGeneratedCardToCombat(card, dest, player);
    }

    /// <summary>
    /// Populates the player's Combat Collection pile from their persistent CollectorRunState Collection.
    /// Shuffles with CombatCardGeneration RNG and places any Bottled Collection card on top (index 0).
    /// </summary>
    public static void InitializeCombatCollection(ICombatState combatState, Player player)
    {
        var runState = CollectorRunState.GetOrCreate(player);
        var data = CollectorCombatState.Get(combatState).GetPlayer(player);
        data.CombatCollection.Clear();
        data.CollectedCardsPlayedThisCombat = 0;
        data.BlockBeforeTurnStartClear = 0;
        data.LastTurnUnusedBlock = 0;

        CardModel? bottledCard = null;
        var entries = runState.Collection;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var canonical = ModelDb.GetByIdOrNull<CardModel>(entry.CardId);
            if (canonical is null)
                continue;

            var card = combatState.CreateCard(canonical, player);
            for (int u = 0; u < entry.UpgradeLevel && card.IsUpgradable; u++)
                card.UpgradeInternal();

            if (i == runState.BottledCollectionIndex)
                bottledCard = card;
            else
                data.CombatCollection.Add(card);
        }

        // Fisher-Yates shuffle using combat RNG.
        var rng = player.RunState.Rng.CombatCardGeneration;
        for (int i = data.CombatCollection.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (data.CombatCollection[i], data.CombatCollection[j]) = (data.CombatCollection[j], data.CombatCollection[i]);
        }

        if (bottledCard is not null)
        {
            data.CombatCollection.Insert(0, bottledCard);
        }
    }

    /// <summary>
    /// Draws 1 card from the player's Combat Collection (returning the drawn card for Soulforge, or null).
    /// Guards NoDrawPower and full hand BEFORE removing from CombatCollection so a blocked draw never strands
    /// a Collectible card in the main Draw Pile.
    /// </summary>
    public static async Task<CardModel?> DrawFromCollectionAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        bool removeExhaust = false)
    {
        if (player.PlayerCombatState is not { } pcs
            || player.Creature.CombatState is not { } combatState
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return null;
        }

        if (player.Creature.HasPower<NoDrawPower>())
            return null;

        if (pcs.Hand.Cards.Count >= CardPile.MaxCardsInHand)
            return null;

        var data = CollectorCombatState.Get(combatState).GetPlayer(player);
        CardModel? drawn;
        if (data.CombatCollection.Count > 0)
        {
            drawn = data.CombatCollection[0];
            data.CombatCollection.RemoveAt(0);
        }
        else
        {
            drawn = null;
            foreach (var relic in player.Relics)
            {
                if (!relic.IsMelted && relic is IEmptyCollectionDrawProvider provider)
                {
                    drawn = provider.TryCreateFallbackCollectible(combatState, player);
                    if (drawn is not null)
                        break;
                }
            }
            if (drawn is null)
                return null;
        }

        if (removeExhaust && drawn.Keywords.Contains(CardKeyword.Exhaust))
            drawn.RemoveKeyword(CardKeyword.Exhaust);

        await CardPileCmd.Add(drawn, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(choiceContext, 1m, player);
        return drawn;
    }

    public static int GetTurnStartCollectionDrawCount(Player player)
    {
        int count = 1;
        foreach (var power in player.Creature.Powers)
        {
            if (power is IExtraCollectionDrawProvider provider)
                count += provider.ExtraCollectionDraws;
        }
        return count;
    }

    /// <summary>
    /// Deterministically maps an enemy creature or encounter room to a curated Collectible card.
    /// </summary>
    public static CardModel CreateCollectibleForEnemy(Player player, Creature? enemy, RoomType roomType = RoomType.Monster)
    {
        Type[] pool = roomType switch
        {
            RoomType.Boss => BossCollectibles,
            RoomType.Elite => EliteCollectibles,
            _ => NormalCollectibles
        };

        string key = enemy?.Monster?.Id.Entry ?? enemy?.Name ?? roomType.ToString();
        int hash = 17;
        foreach (char ch in key)
            hash = unchecked(hash * 31 + ch);
        int idx = (int)((uint)hash % (uint)pool.Length);

        var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(pool[idx]));
        return player.Creature.CombatState is { } combatState
            ? combatState.CreateCard(canonical, player)
            : player.RunState.CreateCard(canonical, player);
    }

    public static CardModel CreateCollectibleForRoom(Player player, CombatRoom room)
    {
        var primaryEnemy = room.CombatState?.Enemies.FirstOrDefault();
        return CreateCollectibleForEnemy(player, primaryEnemy, room.RoomType);
    }

    public static int GetEssenceRewardForRoom(RoomType roomType) => roomType switch
    {
        RoomType.Boss => 3,
        RoomType.Elite => 2,
        _ => 1
    };

    public static CardModel CreateRandomCollectible(ICombatState combatState, Player player)
    {
        var rng = player.RunState.Rng.CombatCardGeneration;
        var type = AllPoolCollectibleTypes[rng.NextInt(AllPoolCollectibleTypes.Count)];
        var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
        return combatState.CreateCard(canonical, player);
    }
}
