using Downfall.Snecko;
using Downfall.Snecko.Cards;
using Downfall.Snecko.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Testing;

internal static class SneckoTests
{
    internal static Task TestVisualsRuntime() =>
        DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheSnecko>("SNECKO_VIS01");

    internal static async Task TestStarterCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSnecko>("SNECKO_START01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "Snecko PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");
        await CreatureCmd.GainMaxHp(enemy, 300m);

        // 1. Starter stats, deck, and Turn-1 SneckoSoul -> SoulRoll in hand (6 cards total!).
        AutoTestAssert.Equal(85, player.Creature.MaxHp, "Snecko StartingHp is 85");
        AutoTestAssert.Equal(10, player.Deck.Cards.Count, "Snecko starter deck has 10 cards");
        AutoTestAssert.True(player.Relics.OfType<SneckoSoul>().Any(), "Player starts with SneckoSoul");
        var soulRoll = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<SoulRoll>().FirstOrDefault(), "SneckoSoul added SoulRoll to Turn-1 hand");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "Turn-1 hand has 5 drawn + 1 SoulRoll = 6 cards");

        // 2. SoulRoll: 0 cost, Retain, Exhaust, gains 3 Block, Muddles a chosen card to a different cost in 0..3.
        var muddleTarget = pcs.Hand.Cards.First(c => c is not SoulRoll);
        int costBefore = muddleTarget.EnergyCost.GetWithModifiers(CostModifiers.All);
        int blockBefore = player.Creature.Block;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, muddleTarget)))
        {
            AutoTestAssert.True(soulRoll.TryManualPlay(null), "Play SoulRoll");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(blockBefore + 3, player.Creature.Block, "SoulRoll granted 3 Block");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(soulRoll), "SoulRoll exhausted");
        int costAfter = muddleTarget.EnergyCost.GetWithModifiers(CostModifiers.All);
        AutoTestAssert.True(costAfter is >= 0 and <= 3 && costAfter != costBefore,
            $"SoulRoll muddled cost from {costBefore} to different value in 0..3 (got {costAfter})");

        // 3. Overflow boundary test on TailWhip: 5 cards in hand -> NO gold glow, NO Weak/Vulnerable!
        await ClearHandAndRefillEnergy(player, pcs);
        var tailWhip5 = combatState.CreateCard<TailWhip>(player);
        await CardPileCmd.Add(tailWhip5, PileType.Hand);
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        AutoTestAssert.Equal(5, pcs.Hand.Cards.Count, "Hand has exactly 5 cards");
        AutoTestAssert.True(!tailWhip5.ShouldGlowGold, "TailWhip does NOT glow gold at 5 cards in hand");
        int hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(tailWhip5.TryManualPlay(enemy), "Play TailWhip at 5 cards");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 10, enemy.CurrentHp, "TailWhip dealt 10 damage");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<WeakPower>(), "TailWhip did NOT apply Weak at 5 cards");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<VulnerablePower>(), "TailWhip did NOT apply Vulnerable at 5 cards");

        // 4. Overflow active at 6 cards in hand: TailWhip+ glows gold & applies 2 Weak + 2 Vulnerable!
        await ClearHandAndRefillEnergy(player, pcs);
        var tailWhip6 = combatState.CreateCard<TailWhip>(player);
        CardCmd.Upgrade(tailWhip6);
        await CardPileCmd.Add(tailWhip6, PileType.Hand);
        for (int i = 0; i < 5; i++)
            await CardPileCmd.Add(combatState.CreateCard<SneckoDefend>(player), PileType.Hand);
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "Hand has 6 cards");
        AutoTestAssert.True(tailWhip6.ShouldGlowGold, "TailWhip+ glows gold at 6 cards in hand");
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(tailWhip6.TryManualPlay(enemy), "Play TailWhip+ at 6 cards (Overflow!)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 12, enemy.CurrentHp, "TailWhip+ dealt 12 damage");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "TailWhip+ Overflow applied 2 Weak");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "TailWhip+ Overflow applied 2 Vulnerable");

        // 5. SnekBite+ deals 9 damage (with Vulnerable -> floor(9 * 1.5) = 13) and muddles 2 cards in hand!
        await ClearHandAndRefillEnergy(player, pcs);
        var snekBite = combatState.CreateCard<SnekBite>(player);
        CardCmd.Upgrade(snekBite);
        var targetA = combatState.CreateCard<SneckoStrike>(player);
        var targetB = combatState.CreateCard<SneckoDefend>(player);
        await CardPileCmd.Add(snekBite, PileType.Hand);
        await CardPileCmd.Add(targetA, PileType.Hand);
        await CardPileCmd.Add(targetB, PileType.Hand);
        int costA0 = targetA.EnergyCost.GetWithModifiers(CostModifiers.All);
        int costB0 = targetB.EnergyCost.GetWithModifiers(CostModifiers.All);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(snekBite.TryManualPlay(enemy), "Play SnekBite+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 13, enemy.CurrentHp, "SnekBite+ dealt 13 damage (9 * 1.5 Vulnerable)");
        int costA1 = targetA.EnergyCost.GetWithModifiers(CostModifiers.All);
        int costB1 = targetB.EnergyCost.GetWithModifiers(CostModifiers.All);
        AutoTestAssert.True(costA1 != costA0 && costA1 is >= 0 and <= 3, $"SnekBite+ muddled card A ({costA0} -> {costA1})");
        AutoTestAssert.True(costB1 != costB0 && costB1 is >= 0 and <= 3, $"SnekBite+ muddled card B ({costB0} -> {costB1})");

        // 6. Verify offclass & debuff card pools have >= 3 distinct cards for all Gift tiers!
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Common).Count >= 3, "Common offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Uncommon).Count >= 3, "Uncommon offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Rare).Count >= 3, "Rare offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Common && SneckoOffclass.IsDebuffCard(c)).Count >= 3, "Common debuff offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, c => c.Rarity == CardRarity.Uncommon && SneckoOffclass.IsDebuffCard(c)).Count >= 3, "Uncommon debuff offclass pool >= 3");
        AutoTestAssert.True(SneckoOffclass.GetOffclassPool(player, inCombat: false, SneckoOffclass.IsDebuffCard).Count >= 3, "Any debuff offclass pool >= 3");

        // 7. Muddled cost expires cleanly at turn end (next turn)!
        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(1, targetA.EnergyCost.GetWithModifiers(CostModifiers.All), "Muddled cost expired after turn end");
        AutoTestAssert.True(!pcs.Hand.Cards.OfType<SoulRoll>().Any(), "SneckoSoul does not re-add SoulRoll on Turn 2");

        // 8. TouchOfOrobas upgrades SneckoSoul -> SuperSneckoSoul (Starter rarity, Orobas-only).
        AutoTestAssert.Equal(RelicRarity.Starter, ModelDb.Relic<SuperSneckoSoul>().Rarity, "SuperSneckoSoul is Starter rarity");
        var orobas = (MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas)ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas>().ToMutable();
        AutoTestAssert.True(orobas.SetupForPlayer(player), "TouchOfOrobas found SneckoSoul");
        AutoTestAssert.Equal(ModelDb.Relic<SuperSneckoSoul>().Id, orobas.UpgradedRelic, "TouchOfOrobas upgrades SneckoSoul -> SuperSneckoSoul");
        await RelicCmd.Obtain(orobas, player);
        AutoTestAssert.True(player.Relics.OfType<SuperSneckoSoul>().Any(), "TouchOfOrobas granted SuperSneckoSoul");
        AutoTestAssert.True(!player.Relics.OfType<SneckoSoul>().Any(), "TouchOfOrobas replaced SneckoSoul");

        // 9. SuperSneckoSoul alternating turns: Round 3 adds SoulRoll (6 cards), Round 4 draws 1 extra card (6 cards) & muddles it (no SoulRoll)!
        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        var round3SoulRoll = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<SoulRoll>().FirstOrDefault(), "SuperSneckoSoul added SoulRoll on Round 3");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "SuperSneckoSoul Round 3 hand size = 5 drawn + 1 SoulRoll");
        // Discard SoulRoll so Round 4 doesn't retain it, making Round 4's draw+muddle assertion exact.
        await CardPileCmd.Add(round3SoulRoll, PileType.Exhaust);

        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 4);
        AutoTestAssert.True(!pcs.Hand.Cards.OfType<SoulRoll>().Any(), "SuperSneckoSoul did NOT add SoulRoll on alternate Round 4");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "SuperSneckoSoul drew 1 extra card (6 total) on Round 4");
        AutoTestAssert.True(
            pcs.Hand.Cards.Any(c => c.EnergyCost.GetWithModifiers(CostModifiers.All) != c.EnergyCost.Canonical),
            "SuperSneckoSoul muddled the extra drawn card on Round 4");
    }

    internal static async Task ClearHandAndRefillEnergy(MegaCrit.Sts2.Core.Entities.Players.Player player, MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState pcs)
    {
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        if (pcs.Energy < 10)
            await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
    }
}
