using Downfall.Guardian.Orbs;
using Downfall.Guardian.Powers;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian;

internal interface IInStasisCard
{
    Task OnEnteredStasisAsync(PlayerChoiceContext choiceContext, StasisOrb orb) => Task.CompletedTask;
    Task OnTurnStartInStasisAsync(PlayerChoiceContext choiceContext, StasisOrb orb) => Task.CompletedTask;
    Task OnEvokeFromStasisAsync(PlayerChoiceContext choiceContext, StasisOrb orb) => Task.CompletedTask;
}

internal interface IStasisEnterListener
{
    Task OnCardEnteredStasisAsync(PlayerChoiceContext choiceContext, StasisOrb orb, CardModel card);
}

internal interface IStasisUpgradeProvider
{
    bool ShouldUpgradeOnStasis => true;
}

internal interface IModeShiftTriggerListener
{
    Task OnModeShiftTriggeredAsync(PlayerChoiceContext choiceContext, Player player);
}

internal interface IBraceBonusProvider
{
    int ExtraBraceAmount { get; }
    void OnBraceBonusApplied() { }
}

internal static class GuardianMechanics
{
    public const string IdleSpritePath = "res://Downfall/images/guardian/char/guardian.png";
    public const string DefensiveSpritePath = "res://Downfall/images/guardian/char/guardian_defensive.png";

    public static bool IsValidGuardianSpritePath(string? path) =>
        path is IdleSpritePath or DefensiveSpritePath;

    internal static List<StasisOrb> GetStasisOrbs(Player? player) =>
        player?.PlayerCombatState?.OrbQueue.Orbs.OfType<StasisOrb>().ToList() ?? [];

    internal static bool CanPlaceInStasis(Player? player)
    {
        if (player?.PlayerCombatState is not { } pcs)
            return false;

        if (pcs.OrbQueue.Capacity <= 0)
            return false;

        return pcs.OrbQueue.Orbs.Count < pcs.OrbQueue.Capacity
            || pcs.OrbQueue.Orbs.Any(o => o is not StasisOrb);
    }

    internal static async Task<StasisOrb?> PlaceInStasisAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        CardModel card,
        bool isSelfStasis = false,
        bool placedBeforeTurnOne = false)
    {
        if (!player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || player.PlayerCombatState is not { } pcs
            || !CanPlaceInStasis(player))
        {
            return null;
        }

        // If orb slots are full, evict oldest non-Stasis orb first; never overwrite an existing StasisOrb.
        if (pcs.OrbQueue.Orbs.Count >= pcs.OrbQueue.Capacity)
        {
            var nonStasis = pcs.OrbQueue.Orbs.FirstOrDefault(o => o is not StasisOrb);
            if (nonStasis is null)
                return null;

            await EvokeSpecificOrbAsync(choiceContext, player, nonStasis);
            if (pcs.OrbQueue.Orbs.Count >= pcs.OrbQueue.Capacity || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return null;
        }

        PileType? previousPileType = null;
        if (card.Pile is { IsCombatPile: true } oldPile)
        {
            previousPileType = oldPile.Type;
            if (oldPile.Type == PileType.Hand)
                NCombatRoom.Instance?.Ui.Hand?.Remove(card);
            card.RemoveFromCurrentPile();
        }

        var orb = (StasisOrb)ModelDb.Orb<StasisOrb>().ToMutable();
        orb.InitializeCard(card, isSelfStasis, placedBeforeTurnOne);

        await OrbCmd.Channel(choiceContext, orb, player);
        if (!pcs.OrbQueue.Orbs.Contains(orb))
        {
            if (previousPileType is { } restorePile)
            {
                var fallback = restorePile == PileType.Hand && pcs.Hand.Cards.Count >= 10
                    ? PileType.Discard
                    : restorePile;
                await CardPileCmd.Add(card, fallback);
            }
            return null;
        }

        if (card.IsUpgradable && player.Relics.OfType<IStasisUpgradeProvider>().Any(p => p.ShouldUpgradeOnStasis))
        {
            CardCmd.Upgrade(card);
        }

        if (card is IInStasisCard inStasis)
        {
            await inStasis.OnEnteredStasisAsync(choiceContext, orb);
        }

        RefreshOrbVisuals(player);

        foreach (var listener in player.Creature.Powers.OfType<IStasisEnterListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnCardEnteredStasisAsync(choiceContext, orb, card);
        }

        foreach (var listener in player.Relics.OfType<IStasisEnterListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnCardEnteredStasisAsync(choiceContext, orb, card);
        }

        return orb;
    }

    internal static async Task<bool> EvokeSpecificOrbAsync(PlayerChoiceContext choiceContext, Player player, OrbModel orb)
    {
        if (CombatManager.Instance.IsOverOrEnding || player.PlayerCombatState is not { } pcs)
            return false;

        if (!pcs.OrbQueue.Remove(orb))
            return false;

        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.EvokeOrbAnim(orb);
        IEnumerable<Creature> targets;
        choiceContext.PushModel(orb);
        try
        {
            targets = await orb.Evoke(choiceContext);
        }
        finally
        {
            choiceContext.PopModel(orb);
        }

        if (player.Creature.CombatState is not null)
        {
            await Hook.AfterOrbEvoked(choiceContext, player.Creature.CombatState, orb, targets);
            orb.RemoveInternal();
        }
        return true;
    }

    internal static async Task<bool> EvokeStasisIfHandHasRoomAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        StasisOrb orb)
    {
        if (player.PlayerCombatState is not { } pcs || !pcs.OrbQueue.Orbs.Contains(orb))
            return false;

        bool isVolatile = orb.StasisCard is GuardianCard gc && gc.IsVolatile;
        if (!isVolatile && pcs.Hand.Cards.Count >= 10)
            return false;

        return await EvokeSpecificOrbAsync(choiceContext, player, orb);
    }

    /// <summary>
    /// Accelerates (ticks down by 1) the oldest Stasis orb <paramref name="times"/> times.
    /// </summary>
    internal static async Task AccelerateAsync(PlayerChoiceContext choiceContext, Player player, int times = 1)
    {
        for (int i = 0; i < times; i++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var oldest = GetStasisOrbs(player).FirstOrDefault();
            if (oldest is null)
                break;

            await oldest.TickTurnAsync(choiceContext);
        }
    }

    /// <summary>
    /// Accelerates (ticks down by 1) ALL current Stasis orbs.
    /// </summary>
    internal static async Task AccelerateAllAsync(PlayerChoiceContext choiceContext, Player player, int times = 1)
    {
        for (int t = 0; t < times; t++)
        {
            foreach (var orb in GetStasisOrbs(player))
            {
                if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    return;
                if (player.PlayerCombatState?.OrbQueue.Orbs.Contains(orb) != true)
                    continue;

                await orb.TickTurnAsync(choiceContext);
            }
        }
    }

    internal static async Task BraceAsync(PlayerChoiceContext choiceContext, Player player, int amount)
    {
        if (amount <= 0 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int bonus = 0;
        foreach (var provider in player.Relics.OfType<IBraceBonusProvider>())
        {
            if (provider.ExtraBraceAmount > 0)
            {
                bonus += provider.ExtraBraceAmount;
                provider.OnBraceBonusApplied();
            }
        }
        int totalAmount = amount + bonus;

        var modeShift = player.Creature.GetPower<ModeShiftPower>();
        if (modeShift is null)
        {
            await PowerCmd.Apply<ModeShiftPower>(choiceContext, player.Creature, ModeShiftPower.StartingThreshold, player.Creature, null);
            modeShift = player.Creature.GetPower<ModeShiftPower>();
        }

        if (modeShift is not null)
        {
            await modeShift.ReduceAsync(choiceContext, totalAmount);
        }
    }

    internal static async Task EnterDefensiveModeAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int turns,
        int baseBlock = 0,
        bool triggerModeShiftListeners = true)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (baseBlock > 0)
        {
            await CreatureCmd.GainBlock(player.Creature, baseBlock, ValueProp.Unpowered, null);
        }

        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<DefensiveModePower>(choiceContext, player.Creature, Math.Max(1, turns), player.Creature, null);

        if (triggerModeShiftListeners)
        {
            foreach (var listener in player.Relics.OfType<IModeShiftTriggerListener>().ToList())
            {
                if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    break;
                await listener.OnModeShiftTriggeredAsync(choiceContext, player);
            }

            foreach (var listener in player.Creature.Powers.OfType<IModeShiftTriggerListener>().ToList())
            {
                if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    break;
                await listener.OnModeShiftTriggeredAsync(choiceContext, player);
            }
        }
    }

    internal static async Task ReduceDebuffsAsync(
        PlayerChoiceContext choiceContext,
        Creature target,
        int amount,
        CardModel? cardSource)
    {
        if (amount <= 0 || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        foreach (var power in target.Powers.ToList())
        {
            if (!target.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            if (!target.Powers.Contains(power))
                continue;

            bool isDebuff = power.Type == PowerType.Debuff || (power.AllowNegative && power.Amount < 0);
            if (!isDebuff)
                continue;

            if (power.StackType == PowerStackType.Single)
            {
                await PowerCmd.Remove(power);
            }
            else if (power.Amount > 0)
            {
                if (power.Amount <= amount)
                    await PowerCmd.Remove(power);
                else
                    await PowerCmd.ModifyAmount(choiceContext, power, -amount, target, cardSource);
            }
            else if (power.Amount < 0)
            {
                if (-power.Amount <= amount)
                    await PowerCmd.Remove(power);
                else
                    await PowerCmd.ModifyAmount(choiceContext, power, amount, target, cardSource);
            }
        }
    }

    internal static void SwapGuardianSprite(Creature creature, bool defensive)
    {
        if (creature.Player?.Character is not TheGuardian)
            return;

        var sprite = NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals?.Body as Sprite2D;
        if (sprite is null)
            return;

        string path = defensive ? DefensiveSpritePath : IdleSpritePath;
        if (ResourceLoader.Load<Texture2D>(path) is { } texture)
        {
            sprite.Texture = texture;
        }
    }

    internal static async Task ChooseAndPlaceRandomCardInStasisAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int choiceCount = 3,
        bool canSkip = false)
    {
        if (!player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || player.Creature.CombatState is not { } combatState
            || !CanPlaceInStasis(player))
        {
            return;
        }

        var pool = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanBeGeneratedInCombat && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();
        if (pool.Count == 0)
            return;

        var rng = player.RunState.Rng.CombatCardGeneration;
        var choices = new List<CardModel>(Math.Min(choiceCount, pool.Count));
        for (int i = 0; i < choiceCount && pool.Count > 0; i++)
        {
            int idx = rng.NextInt(pool.Count);
            choices.Add(combatState.CreateCard(pool[idx], player));
            pool.RemoveAt(idx);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, player, canSkip: canSkip);
        if (chosen is not null && player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PlaceInStasisAsync(choiceContext, player, chosen);
        }
    }

    internal static void RefreshOrbVisuals(Player? player)
    {
        if (player?.Creature is not null)
        {
            NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.UpdateVisuals(OrbEvokeType.None);
        }
    }
}
