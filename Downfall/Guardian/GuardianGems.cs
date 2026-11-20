using Downfall.Guardian.Cards;
using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace Downfall.Guardian;

public enum GemType
{
    Red,
    Green,
    Lightblue,
    Blue,
    Fragmented,
    Cyan,
    Crimson,
    White,
    Yellow,
    Purple,
    Orange,
    Synthetic
}

internal static class GuardianGems
{
    private static readonly GemType[] CommonGems =
    [
        GemType.Red,
        GemType.Lightblue,
        GemType.Fragmented,
        GemType.Blue
    ];

    private static readonly GemType[] AllGems =
    [
        GemType.Red,
        GemType.Green,
        GemType.Lightblue,
        GemType.Blue,
        GemType.Fragmented,
        GemType.Cyan,
        GemType.Crimson,
        GemType.White,
        GemType.Yellow,
        GemType.Purple,
        GemType.Orange,
        GemType.Synthetic
    ];

    internal static List<GemType> ParseCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        var result = new List<GemType>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<GemType>(part, ignoreCase: true, out var gem))
                result.Add(gem);
        }
        return result;
    }

    internal static string ToCsv(IEnumerable<GemType> gems) =>
        string.Join(",", gems.Select(g => g.ToString()));

    internal static string GetDisplayName(GemType gem) => gem switch
    {
        GemType.Red => "Ruby",
        GemType.Green => "Emerald",
        GemType.Lightblue => "Tourmaline",
        GemType.Blue => "Sapphire",
        GemType.Fragmented => "Fragmented Gem",
        GemType.Cyan => "Aquamarine",
        GemType.Crimson => "Garnet",
        GemType.White => "Opal",
        GemType.Yellow => "Amber",
        GemType.Purple => "Amethyst",
        GemType.Orange => "Citrine",
        GemType.Synthetic => "Onyx",
        _ => gem.ToString()
    };

    internal static string GetShortEffect(GemType gem) => gem switch
    {
        GemType.Red => "Gain 2 temp. Strength.",
        GemType.Green => "Gain 2 temp. Dexterity.",
        GemType.Lightblue => "Gain 4 temp. Thorns.",
        GemType.Blue => "Brace 4.",
        GemType.Fragmented => "Add a Crystal Shiv to your Hand.",
        GemType.Cyan => "Add a Crystal Ward to your Hand.",
        GemType.Crimson => "Apply 1 Vulnerable to ALL enemies.",
        GemType.White => "Draw 1 card.",
        GemType.Yellow => "Accelerate.",
        GemType.Purple => "ALL enemies lose 2 Strength this turn.",
        GemType.Orange => "Gain 1 Energy.",
        GemType.Synthetic => "Reduce each of your debuffs by 1.",
        _ => string.Empty
    };

    internal static CardModel GetCanonicalGemCard(GemType gem) => gem switch
    {
        GemType.Red => ModelDb.Card<GemRed>(),
        GemType.Green => ModelDb.Card<GemGreen>(),
        GemType.Lightblue => ModelDb.Card<GemLightblue>(),
        GemType.Blue => ModelDb.Card<GemBlue>(),
        GemType.Fragmented => ModelDb.Card<GemFragmented>(),
        GemType.Cyan => ModelDb.Card<GemCyan>(),
        GemType.Crimson => ModelDb.Card<GemCrimson>(),
        GemType.White => ModelDb.Card<GemWhite>(),
        GemType.Yellow => ModelDb.Card<GemYellow>(),
        GemType.Purple => ModelDb.Card<GemPurple>(),
        GemType.Orange => ModelDb.Card<GemOrange>(),
        GemType.Synthetic => ModelDb.Card<GemSynthetic>(),
        _ => ModelDb.Card<GemRed>()
    };

    internal static CardModel CreateRandomGemCardForDeck(Player owner, bool onlyCommon, Rng rng)
    {
        var pool = onlyCommon ? CommonGems : AllGems;
        var gem = pool[rng.NextInt(pool.Length)];
        return owner.RunState.CreateCard(GetCanonicalGemCard(gem), owner);
    }

    internal static List<CardModel> CreateDistinctGemChoicesForDeck(Player owner, int count, bool onlyCommon, Rng rng)
    {
        var pool = (onlyCommon ? CommonGems : AllGems).ToList();
        var result = new List<CardModel>(Math.Min(count, pool.Count));
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = rng.NextInt(pool.Count);
            result.Add(owner.RunState.CreateCard(GetCanonicalGemCard(pool[idx]), owner));
            pool.RemoveAt(idx);
        }
        return result;
    }

    internal static CardReward CreateGemCardReward(Player owner, int choiceCount = 3)
    {
        var cards = CreateDistinctGemChoicesForDeck(owner, choiceCount, onlyCommon: false, owner.RunState.Rng.CombatCardGeneration);
        var rerollOptions = new CardCreationOptions(
            [ModelDb.CardPool<GuardianCardPool>()],
            CardCreationSource.Encounter,
            CardRarityOddsType.RegularEncounter);
        return new CardReward(cards, CardCreationSource.Encounter, owner, rerollOptions);
    }

    internal static async Task TriggerGemAsync(
        GemType gem,
        PlayerChoiceContext choiceContext,
        Player owner,
        CardPlay? cardPlay)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding || owner.Creature.CombatState is not { } combatState)
            return;

        switch (gem)
        {
            case GemType.Red:
                await PowerCmd.Apply<FlexPotionPower>(choiceContext, owner.Creature, 2m, owner.Creature, cardPlay?.Card);
                break;

            case GemType.Green:
                await PowerCmd.Apply<SpeedPotionPower>(choiceContext, owner.Creature, 2m, owner.Creature, cardPlay?.Card);
                break;

            case GemType.Lightblue:
                await PowerCmd.Apply<SlimeTempThornsPower>(choiceContext, owner.Creature, 4m, owner.Creature, cardPlay?.Card);
                break;

            case GemType.Blue:
                await GuardianMechanics.BraceAsync(choiceContext, owner, 4);
                break;

            case GemType.Fragmented:
            {
                var shiv = combatState.CreateCard<CrystalShiv>(owner);
                await CardPileCmd.AddGeneratedCardToCombat(shiv, PileType.Hand, owner);
                break;
            }

            case GemType.Cyan:
            {
                var ward = combatState.CreateCard<CrystalWard>(owner);
                await CardPileCmd.AddGeneratedCardToCombat(ward, PileType.Hand, owner);
                break;
            }

            case GemType.Crimson:
                foreach (var enemy in combatState.HittableEnemies.Where(e => e.IsAlive).ToList())
                {
                    if (CombatManager.Instance.IsOverOrEnding)
                        break;
                    await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy, 1m, owner.Creature, cardPlay?.Card);
                }
                break;

            case GemType.White:
                await CardPileCmd.Draw(choiceContext, 1m, owner);
                break;

            case GemType.Yellow:
                await GuardianMechanics.AccelerateAsync(choiceContext, owner, 1);
                break;

            case GemType.Purple:
                foreach (var enemy in combatState.HittableEnemies.Where(e => e.IsAlive).ToList())
                {
                    if (CombatManager.Instance.IsOverOrEnding)
                        break;
                    await PowerCmd.Apply<PiercingWailPower>(choiceContext, enemy, 2m, owner.Creature, cardPlay?.Card);
                }
                break;

            case GemType.Orange:
                await PlayerCmd.GainEnergy(1m, owner);
                break;

            case GemType.Synthetic:
                await GuardianMechanics.ReduceDebuffsAsync(choiceContext, owner.Creature, 1, cardPlay?.Card);
                break;
        }
    }
}
