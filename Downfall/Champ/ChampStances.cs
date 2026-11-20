using Downfall.Champ.Cards;
using Downfall.Champ.Powers;
using Downfall.Champ.Relics;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ;

public enum ChampStanceKind
{
    None = 0,
    Berserker = 1,
    Defensive = 2,
    Ultimate = 3
}

public interface IChampStanceChangedListener
{
    Task OnStanceChangedAsync(PlayerChoiceContext choiceContext, ChampStanceKind previous, ChampStanceKind current);
}

internal static class ChampStances
{
    public const decimal BaseBerserkerVigor = 2m;
    public const decimal BaseDefensiveCounter = 3m;
    public const decimal BaseBerserkerFinisherStrength = 1m;
    public const decimal BaseDefensiveFinisherBlock = 6m;

    public static string SpriteTexturePathFor(ChampStanceKind stance) => stance switch
    {
        ChampStanceKind.Berserker => "res://Downfall/images/champ/char/champ_berserker.png",
        ChampStanceKind.Defensive => "res://Downfall/images/champ/char/champ_defensive.png",
        ChampStanceKind.Ultimate => "res://Downfall/images/champ/char/champ_ultimate.png",
        _ => "res://Downfall/images/champ/char/champ.png"
    };

    public static bool IsValidChampSpritePath(string? path) =>
        path is "res://Downfall/images/champ/char/champ.png"
            or "res://Downfall/images/champ/char/champ_berserker.png"
            or "res://Downfall/images/champ/char/champ_defensive.png"
            or "res://Downfall/images/champ/char/champ_ultimate.png";

    public static ChampStancePower? GetActiveStancePower(Creature? creature) =>
        creature?.Powers.OfType<ChampStancePower>().FirstOrDefault();

    public static ChampStanceKind GetCurrentStance(Creature? creature) =>
        GetActiveStancePower(creature)?.StanceKind ?? ChampStanceKind.None;

    public static bool IsInStance(Creature? creature) =>
        GetCurrentStance(creature) != ChampStanceKind.None;

    public static bool IsInBerserker(Creature? creature) =>
        GetCurrentStance(creature) is ChampStanceKind.Berserker or ChampStanceKind.Ultimate;

    public static bool IsInDefensive(Creature? creature) =>
        GetCurrentStance(creature) is ChampStanceKind.Defensive or ChampStanceKind.Ultimate;

    public static async Task<bool> EnterStanceAsync(
        Player player,
        ChampStanceKind target,
        PlayerChoiceContext choiceContext)
    {
        if (target == ChampStanceKind.None)
            return await ExitStanceAsync(player, choiceContext);

        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        ChampStanceKind previous = GetCurrentStance(player.Creature);
        if (previous == target)
            return false;

        // UltimateFormPower locks the player in Ultimate Stance.
        if (target != ChampStanceKind.Ultimate && player.Creature.HasPower<UltimateFormPower>())
            return false;

        foreach (var existing in player.Creature.Powers.OfType<ChampStancePower>().ToList())
        {
            await PowerCmd.Remove(existing);
        }

        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        switch (target)
        {
            case ChampStanceKind.Berserker:
                await PowerCmd.Apply<BerserkerStancePower>(choiceContext, player.Creature, 1m, player.Creature, null);
                break;
            case ChampStanceKind.Defensive:
                await PowerCmd.Apply<DefensiveStancePower>(choiceContext, player.Creature, 1m, player.Creature, null);
                break;
            case ChampStanceKind.Ultimate:
                await PowerCmd.Apply<UltimateStancePower>(choiceContext, player.Creature, 1m, player.Creature, null);
                break;
        }

        UpdateCombatSprite(player, target);
        await NotifyStanceChangedAsync(player, choiceContext, previous, target);
        return true;
    }

    public static async Task<bool> ExitStanceAsync(Player player, PlayerChoiceContext choiceContext)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        ChampStanceKind previous = GetCurrentStance(player.Creature);
        if (previous == ChampStanceKind.None)
            return false;

        if (player.Creature.HasPower<UltimateFormPower>())
            return false;

        foreach (var existing in player.Creature.Powers.OfType<ChampStancePower>().ToList())
        {
            await PowerCmd.Remove(existing);
        }

        UpdateCombatSprite(player, ChampStanceKind.None);
        await NotifyStanceChangedAsync(player, choiceContext, previous, ChampStanceKind.None);
        return true;
    }

    public static async Task<bool> EnterStanceNotInAsync(Player player, PlayerChoiceContext choiceContext)
    {
        if (player.Creature.HasPower<UltimateFormPower>())
            return false;

        ChampStanceKind current = GetCurrentStance(player.Creature);
        ChampStanceKind target = current switch
        {
            ChampStanceKind.Defensive => ChampStanceKind.Berserker,
            ChampStanceKind.Berserker => ChampStanceKind.Defensive,
            _ => player.RunState.Rng.CombatCardGeneration.NextInt(2) == 0
                ? ChampStanceKind.Berserker
                : ChampStanceKind.Defensive
        };
        return await EnterStanceAsync(player, target, choiceContext);
    }

    public static async Task<ChampStanceKind> ChooseStanceAsync(
        Player player,
        ICombatState combatState,
        PlayerChoiceContext choiceContext)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return ChampStanceKind.None;

        var choices = new List<CardModel>(2)
        {
            combatState.CreateCard<ChooseBerserkerStance>(player),
            combatState.CreateCard<ChooseDefensiveStance>(player)
        };

        var picked = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, player);
        if (picked is ChooseBerserkerStance)
        {
            await EnterStanceAsync(player, ChampStanceKind.Berserker, choiceContext);
            return ChampStanceKind.Berserker;
        }
        if (picked is ChooseDefensiveStance)
        {
            await EnterStanceAsync(player, ChampStanceKind.Defensive, choiceContext);
            return ChampStanceKind.Defensive;
        }

        return ChampStanceKind.None;
    }

    public static decimal GetBerserkerVigorAmount(Creature creature) =>
        BaseBerserkerVigor
        + creature.GetPowerAmount<DoubleStylePower>()
        + creature.GetPowerAmount<DoubleStyleThisTurnPower>();

    public static decimal GetDefensiveCounterAmount(Creature creature) =>
        BaseDefensiveCounter
        + creature.GetPowerAmount<DoubleStylePower>()
        + creature.GetPowerAmount<DoubleStyleThisTurnPower>();

    public static async Task TriggerSkillBonusAsync(
        Player player,
        PlayerChoiceContext choiceContext,
        ChampStanceKind? overrideStance = null,
        int times = 1)
    {
        ChampStanceKind stance = overrideStance ?? GetCurrentStance(player.Creature);
        if (stance == ChampStanceKind.None)
            return;

        for (int i = 0; i < times; i++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            if (stance is ChampStanceKind.Berserker or ChampStanceKind.Ultimate)
            {
                await PowerCmd.Apply<VigorPower>(
                    choiceContext,
                    player.Creature,
                    GetBerserkerVigorAmount(player.Creature),
                    player.Creature,
                    null);
            }

            if (stance is ChampStanceKind.Defensive or ChampStanceKind.Ultimate
                && player.Creature.IsAlive
                && !CombatManager.Instance.IsOverOrEnding)
            {
                await PowerCmd.Apply<CounterPower>(
                    choiceContext,
                    player.Creature,
                    GetDefensiveCounterAmount(player.Creature),
                    player.Creature,
                    null);
            }
        }
    }

    public static async Task ExecuteFinisherAsync(
        Player player,
        PlayerChoiceContext choiceContext,
        bool noExit = false,
        int bonusTimes = 1)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        ChampStanceKind stance = GetCurrentStance(player.Creature);
        if (stance == ChampStanceKind.None)
            return;

        for (int i = 0; i < bonusTimes; i++)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            if (stance is ChampStanceKind.Berserker or ChampStanceKind.Ultimate)
            {
                await PowerCmd.Apply<StrengthPower>(
                    choiceContext,
                    player.Creature,
                    BaseBerserkerFinisherStrength,
                    player.Creature,
                    null);
            }

            if (stance is ChampStanceKind.Defensive or ChampStanceKind.Ultimate
                && player.Creature.IsAlive
                && !CombatManager.Instance.IsOverOrEnding)
            {
                decimal blockAmount = BaseDefensiveFinisherBlock;
                foreach (var manual in player.Relics.OfType<DefensiveTrainingManual>())
                {
                    manual.FlashRelic();
                    blockAmount += DefensiveTrainingManual.BonusBlock;
                }
                await CreatureCmd.GainBlock(player.Creature, blockAmount, ValueProp.Unpowered, null);
            }
        }

        var currentCard = CombatManager.Instance.History.CardPlaysStarted
            .LastOrDefault(e => e.CardPlay.Card.Owner == player)?.CardPlay.Card;
        if (!noExit && currentCard is not null)
        {
            var sigRelic = player.Relics.OfType<SignatureFinisher>().FirstOrDefault(r => r.IsSignatureCard(currentCard));
            if (sigRelic is not null)
            {
                sigRelic.FlashRelic();
                noExit = true;
            }
        }

        if (!noExit && player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await ExitStanceAsync(player, choiceContext);
        }
    }

    private static async Task NotifyStanceChangedAsync(
        Player player,
        PlayerChoiceContext choiceContext,
        ChampStanceKind previous,
        ChampStanceKind current)
    {
        foreach (var listener in player.Creature.Powers.OfType<IChampStanceChangedListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;
            await listener.OnStanceChangedAsync(choiceContext, previous, current);
        }

        foreach (var listener in player.Relics.OfType<IChampStanceChangedListener>().ToList())
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;
            await listener.OnStanceChangedAsync(choiceContext, previous, current);
        }
    }

    public static void UpdateCombatSprite(Player player, ChampStanceKind stance)
    {
        if (player.Character is not TheChamp)
            return;

        string texturePath = SpriteTexturePathFor(stance);
        var body = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals?.Body as Sprite2D;
        if (body is null || body.Texture?.ResourcePath == texturePath || !ResourceLoader.Exists(texturePath))
            return;

        if (ResourceLoader.Load<Texture2D>(texturePath) is { } texture)
        {
            body.Texture = texture;
        }
    }
}
