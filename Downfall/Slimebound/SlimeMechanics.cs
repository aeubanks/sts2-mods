using Downfall.Slimebound.Orbs;
using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound;

internal interface IGoopConsumedListener
{
    Task OnGoopConsumedAsync(PlayerChoiceContext choiceContext, Creature target, int consumedAmount);
}

internal interface ISlimeSplitListener
{
    Task OnSlimeSplitAsync(PlayerChoiceContext choiceContext, SlimeOrb slime);
}

internal interface IAcidTongueProvider
{
    int ExtraGoopAmount { get; }
}

internal interface IMinionMasterProvider
{
    int ExtraCommands { get; }
}

internal interface IPreventGoopDecay
{
    Task ConsumePreventionAsync();
}

internal interface ITackleSelfDamagePrevention
{
    Task<bool> TryPreventTackleSelfDamageAsync();
}

internal interface ITackleSelfDamageModifier
{
    int ModifyTackleSelfDamage(int currentDamage);
}

internal static class SlimeMechanics
{
    /// <summary>
    /// Leading Slime = newest (last-channeled) SlimeOrb in the queue, matching DownfallSTS SlimeboundMod.getLeadingSlime().
    /// </summary>
    internal static SlimeOrb? GetLeadingSlime(Player? player) =>
        player?.PlayerCombatState?.OrbQueue.Orbs.OfType<SlimeOrb>().LastOrDefault();

    internal static List<SlimeOrb> GetSlimes(Player? player) =>
        player?.PlayerCombatState?.OrbQueue.Orbs.OfType<SlimeOrb>().ToList() ?? [];

    internal static async Task<T?> SplitSlimeAsync<T>(PlayerChoiceContext choiceContext, Player player) where T : SlimeOrb
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return null;

        var slime = (T)ModelDb.Orb<T>().ToMutable();
        return await SplitSlimeAsync(choiceContext, player, slime) ? slime : null;
    }

    internal static async Task<bool> SplitSlimeAsync(PlayerChoiceContext choiceContext, Player player, SlimeOrb slime)
    {
        if (!player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || player.PlayerCombatState is not { } pcs)
        {
            return false;
        }

        await OrbCmd.Channel(choiceContext, slime, player);
        if (!pcs.OrbQueue.Orbs.Contains(slime))
            return false;

        await NotifySlimeSplitAsync(choiceContext, player, slime);
        return true;
    }

    internal static async Task<SlimeOrb?> SpawnNormalSlimeAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return null;

        SlimeOrb slime = player.RunState.Rng.CombatCardGeneration.NextInt(4) switch
        {
            0 => (SlimeOrb)ModelDb.Orb<AttackSlime>().ToMutable(),
            1 => (SlimeOrb)ModelDb.Orb<PoisonSlime>().ToMutable(),
            2 => (SlimeOrb)ModelDb.Orb<ShieldSlime>().ToMutable(),
            _ => (SlimeOrb)ModelDb.Orb<SlimingSlime>().ToMutable()
        };

        return await SplitSlimeAsync(choiceContext, player, slime) ? slime : null;
    }

    internal static async Task<SlimeOrb?> SpawnSpecialistSlimeAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return null;

        SlimeOrb slime = player.RunState.Rng.CombatCardGeneration.NextInt(8) switch
        {
            0 => (SlimeOrb)ModelDb.Orb<BronzeSlime>().ToMutable(),
            1 => (SlimeOrb)ModelDb.Orb<HexSlime>().ToMutable(),
            2 => (SlimeOrb)ModelDb.Orb<TorchHeadSlime>().ToMutable(),
            3 => (SlimeOrb)ModelDb.Orb<CultistSlime>().ToMutable(),
            4 => (SlimeOrb)ModelDb.Orb<ProtectorSlime>().ToMutable(),
            5 => (SlimeOrb)ModelDb.Orb<ChampSlime>().ToMutable(),
            6 => (SlimeOrb)ModelDb.Orb<DrawingSlime>().ToMutable(),
            _ => (SlimeOrb)ModelDb.Orb<SlowingSlime>().ToMutable()
        };

        return await SplitSlimeAsync(choiceContext, player, slime) ? slime : null;
    }

    /// <summary>
    /// Exhumes up to count random Lick cards from the Exhaust Pile to Hand, preferring non-token Licks
    /// before basic Lick tokens (matching DownfallSTS BURIED priority on Lick).
    /// </summary>
    internal static async Task<int> ExhumeLicksAsync(PlayerChoiceContext choiceContext, Player player, int count)
    {
        if (count <= 0 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding || player.PlayerCombatState is not { } pcs)
            return 0;

        int exhumed = 0;
        var rng = player.RunState.Rng.CombatCardGeneration;
        for (int i = 0; i < count; i++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var nonTokens = pcs.ExhaustPile.Cards.OfType<SlimeCard>().Where(c => c.IsLick && c is not Cards.Lick).ToList();
            var pool = nonTokens.Count > 0
                ? nonTokens
                : pcs.ExhaustPile.Cards.OfType<SlimeCard>().Where(c => c.IsLick).ToList();

            if (pool.Count == 0)
                break;

            var picked = pool[rng.NextInt(pool.Count)];
            var dest = pcs.Hand.Cards.Count >= 10 ? PileType.Discard : PileType.Hand;
            await CardPileCmd.Add(picked, dest);
            exhumed++;
        }
        return exhumed;
    }

    /// <summary>
    /// Triggers the leading (newest) Slime minion's attack. Adds MinionMaster bonus once per Command card play.
    /// </summary>
    internal static async Task CommandAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int count = 1,
        bool applyMinionMaster = true)
    {
        if (count <= 0 || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int extra = applyMinionMaster
            ? player.Creature.Powers.OfType<IMinionMasterProvider>().Sum(p => p.ExtraCommands)
            : 0;
        int total = count + extra;

        for (int i = 0; i < total; i++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var leading = GetLeadingSlime(player);
            if (leading is null)
                break;

            await OrbCmd.Passive(choiceContext, leading, null, countAffectedByHooks: false);
        }
    }

    /// <summary>
    /// Absorbs (evokes) a specific SlimeOrb without ever touching non-Slime Defect orbs.
    /// Mirrors OrbCmd.Evoke's exact order: Remove -> EvokeOrbAnim -> Evoke -> AfterOrbEvoked -> RemoveInternal.
    /// </summary>
    internal static async Task<bool> AbsorbSlimeAsync(PlayerChoiceContext choiceContext, Player player, SlimeOrb slime)
    {
        if (CombatManager.Instance.IsOverOrEnding || player.PlayerCombatState is not { } pcs)
            return false;

        if (!pcs.OrbQueue.Remove(slime))
            return false;

        NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.EvokeOrbAnim(slime);
        IEnumerable<Creature> targets;
        choiceContext.PushModel(slime);
        try
        {
            targets = await slime.Evoke(choiceContext);
        }
        finally
        {
            choiceContext.PopModel(slime);
        }

        if (player.Creature.CombatState is not null)
        {
            await Hook.AfterOrbEvoked(choiceContext, player.Creature.CombatState, slime, targets);
            slime.RemoveInternal();
        }
        return true;
    }

    internal static async Task<bool> AbsorbLeadingSlimeAsync(PlayerChoiceContext choiceContext, Player player)
    {
        var leading = GetLeadingSlime(player);
        return leading is not null && await AbsorbSlimeAsync(choiceContext, player, leading);
    }

    internal static async Task<int> AbsorbAllSlimesAsync(PlayerChoiceContext choiceContext, Player player)
    {
        int absorbed = 0;
        foreach (var slime in GetSlimes(player))
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (await AbsorbSlimeAsync(choiceContext, player, slime))
                absorbed++;
        }
        return absorbed;
    }

    internal static int GetAcidTongueBonus(Player? player) =>
        player?.Creature.Powers.OfType<IAcidTongueProvider>().Sum(p => p.ExtraGoopAmount) ?? 0;

    internal static async Task ApplyGoopAsync(
        PlayerChoiceContext choiceContext,
        Creature target,
        Player applier,
        int baseAmount,
        CardModel? cardSource)
    {
        int total = baseAmount + GetAcidTongueBonus(applier);
        if (total <= 0 || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<SlimedPower>(choiceContext, target, total, applier.Creature, cardSource);
    }

    internal static int CalculateTackleSelfDamage(Player? player, int baseSelfDamage)
    {
        int damage = baseSelfDamage;
        if (player is not null)
        {
            foreach (var mod in player.Creature.Powers.OfType<ITackleSelfDamageModifier>())
                damage = mod.ModifyTackleSelfDamage(damage);
            foreach (var mod in player.Relics.OfType<ITackleSelfDamageModifier>())
                damage = mod.ModifyTackleSelfDamage(damage);
        }
        return Math.Max(0, damage);
    }

    internal static async Task ResolveTackleSelfDamageAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int baseSelfDamage)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int damage = CalculateTackleSelfDamage(player, baseSelfDamage);
        if (damage <= 0)
            return;

        foreach (var preventer in player.Creature.Powers.OfType<ITackleSelfDamagePrevention>().ToList())
        {
            if (await preventer.TryPreventTackleSelfDamageAsync())
                return;
        }

        await CreatureCmd.Damage(choiceContext, player.Creature, damage, ValueProp.Unpowered, player.Creature);
    }

    internal static void RefreshOrbVisuals(Player? player)
    {
        if (player?.Creature is not null)
        {
            NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager?.UpdateVisuals(OrbEvokeType.None);
        }
    }

    internal static CardModel? CreateRandomTackleCard(
        ICombatState combatState,
        Player owner,
        Func<SlimeCard, bool>? filter = null)
    {
        var tackles = ModelDb.CardPool<SlimeboundCardPool>()
            .GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint)
            .OfType<SlimeCard>()
            .Where(c => c.IsTackle && c.CanBeGeneratedInCombat && (filter is null || filter(c)))
            .ToList();
        if (tackles.Count == 0)
            return null;

        var picked = tackles[owner.RunState.Rng.CombatCardGeneration.NextInt(tackles.Count)];
        return combatState.CreateCard(picked, owner);
    }

    private static async Task NotifySlimeSplitAsync(PlayerChoiceContext choiceContext, Player player, SlimeOrb slime)
    {
        foreach (var listener in player.Creature.Powers.OfType<ISlimeSplitListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnSlimeSplitAsync(choiceContext, slime);
        }

        foreach (var listener in player.Relics.OfType<ISlimeSplitListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await listener.OnSlimeSplitAsync(choiceContext, slime);
        }

        if (player.PlayerCombatState is not null)
        {
            foreach (var card in PileType.Discard.GetPile(player).Cards.OfType<ISlimeSplitListener>().ToList())
            {
                if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    break;
                await card.OnSlimeSplitAsync(choiceContext, slime);
            }
        }
    }
}
