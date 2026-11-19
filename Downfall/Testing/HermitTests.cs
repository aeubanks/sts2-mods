using Downfall.Hermit;
using Downfall.Hermit.Cards;
using Downfall.Hermit.Powers;
using Downfall.Hermit.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Testing;

internal static class HermitTests
{
    public static Task TestVisualsRuntime() =>
        DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheHermit>("DFHRMVIS");

    public static async Task TestStarterCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHermit>("DFHRM01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");

        await CreatureCmd.GainMaxHp(enemy, 300m);

        // Starting deck + relic assertions.
        AutoTestAssert.Equal(75, player.Creature.MaxHp, "Hermit starting MaxHp");
        AutoTestAssert.True(player.Relics.Any(r => r is OldLocket), "OldLocket in starting relics");

        // Old Locket adds MementoCard to hand at start of combat (5 drawn + 1 Memento = 6).
        var memento = AutoTestAssert.NotNull(
            pcs.Hand.Cards.OfType<MementoCard>().FirstOrDefault(),
            "OldLocket added MementoCard to Turn 1 hand");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "Turn 1 hand has 5 drawn cards + 1 MementoCard");

        // Test MementoCard Retain across Turn 1 -> Turn 2 and confirm OldLocket does NOT re-trigger.
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.True(pcs.Hand.Cards.Contains(memento), "MementoCard retained into Turn 2");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.OfType<MementoCard>().Count(), "OldLocket did not re-trigger on Turn 2");

        // Play MementoCard: applies 1 Vulnerable to enemy AND to Hermit.
        AutoTestAssert.True(memento.TryManualPlay(null), "Play MementoCard");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "Memento applied 1 Vulnerable to enemy");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<VulnerablePower>(), "Memento applied 1 Vulnerable to Hermit");

        // Clear hand and prepare deterministic Snapshot + Dead On + Concentration + Bruise + Covet + Rugged tests.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await PowerCmd.Remove<VulnerablePower>(enemy);
        await PowerCmd.Remove<VulnerablePower>(player.Creature);
        await PlayerCmd.GainEnergy(10, player);

        // 1. Snapshot NOT Dead On (index 0 of 3 cards): deals 6 damage, grants 0 Block.
        var snapOffCenter = combatState.CreateCard<Snapshot>(player);
        var filler1 = combatState.CreateCard<HermitDefend>(player);
        var filler2 = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(snapOffCenter, PileType.Hand);
        await CardPileCmd.Add(filler1, PileType.Hand);
        await CardPileCmd.Add(filler2, PileType.Hand);
        int hpBefore = enemy.CurrentHp;
        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(snapOffCenter.TryManualPlay(enemy), "Play off-center Snapshot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 6, enemy.CurrentHp, "Off-center Snapshot dealt 6 damage");
        AutoTestAssert.Equal(blockBefore, player.Creature.Block, "Off-center Snapshot gained 0 Block");

        // 2. Snapshot DEAD ON (index 1 of 3 cards) + BruisePower(4) vs enemy with 3 Block:
        //    10 total damage - 3 Block = 7 unblocked damage -> gains 7 Block!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await PowerCmd.Apply<BruisePower>(new ThrowingPlayerChoiceContext(), enemy, 4m, player.Creature, null);
        await CreatureCmd.GainBlock(enemy, 3m, ValueProp.Unpowered, null);
        var leftFiller = combatState.CreateCard<HermitDefend>(player);
        var snapCenter = combatState.CreateCard<Snapshot>(player);
        var rightFiller = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(leftFiller, PileType.Hand);
        await CardPileCmd.Add(snapCenter, PileType.Hand);
        await CardPileCmd.Add(rightFiller, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(snapCenter.TryManualPlay(enemy), "Play centered Dead On Snapshot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 7, enemy.CurrentHp, "Dead On Snapshot + Bruise(4) vs 3 Block dealt 7 unblocked damage");
        AutoTestAssert.Equal(blockBefore + 7, player.Creature.Block, "Dead On Snapshot gained only unblocked damage (7) as Block");

        // 3. ConcentratedPower lets off-center Snapshot trigger Dead On and consumes ConcentratedPower!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await PowerCmd.Remove<BruisePower>(enemy);
        await PowerCmd.Apply<ConcentratedPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1m, player.Creature, null);
        var snapConc = combatState.CreateCard<Snapshot>(player);
        var pad1 = combatState.CreateCard<HermitDefend>(player);
        var pad2 = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(snapConc, PileType.Hand);
        await CardPileCmd.Add(pad1, PileType.Hand);
        await CardPileCmd.Add(pad2, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(snapConc.TryManualPlay(enemy), "Play off-center Snapshot with Concentration");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 6, player.Creature.Block, "Concentrated off-center Snapshot triggered Dead On (+6 Block)");
        AutoTestAssert.True(!player.Creature.HasPower<ConcentratedPower>(), "ConcentratedPower consumed after Dead On card");

        // 4. Covet: exhausting a selected Curse vs discarding a normal card + drawing.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var curseForCovet = combatState.CreateCard<MementoCard>(player);
        var covet = combatState.CreateCard<Covet>(player);
        await CardPileCmd.Add(curseForCovet, PileType.Hand);
        await CardPileCmd.Add(covet, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, curseForCovet)))
        {
            AutoTestAssert.True(covet.TryManualPlay(null), "Play Covet selecting MementoCard Curse");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(curseForCovet), "Covet exhausted selected Curse");
        AutoTestAssert.True(pcs.Hand.Cards.Count >= 1, "Covet drew a card");

        // Normal discard path on Covet+: draws 2 cards and discards chosen non-Curse.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var strikeForCovet = combatState.CreateCard<HermitStrike>(player);
        var covetPlus = combatState.CreateCard<Covet>(player);
        CardCmd.Upgrade(covetPlus);
        await CardPileCmd.Add(strikeForCovet, PileType.Hand);
        await CardPileCmd.Add(covetPlus, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, strikeForCovet)))
        {
            AutoTestAssert.True(covetPlus.TryManualPlay(null), "Play Covet+ selecting Strike");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(PileType.Discard.GetPile(player).Cards.Contains(strikeForCovet), "Covet+ discarded non-Curse Strike");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Covet+ drew 2 cards");

        // 5. RuggedPower ignores <=2 attack damage & unpowered damage, caps >2 unblocked Attack damage to 2, and decrements.
        await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), player.Creature, player.Creature.Block, null);
        await PowerCmd.Apply<RuggedPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1m, player.Creature, null);
        int playerHpBefore = player.Creature.CurrentHp;
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 2m, ValueProp.Move, dealer: enemy, cardSource: null, cardPlay: null);
        AutoTestAssert.Equal(playerHpBefore - 2, player.Creature.CurrentHp, "2 attack damage applied normally");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<RuggedPower>(), "RuggedPower does not decrement on <=2 damage");
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 5m, ValueProp.Unblockable | ValueProp.Unpowered, dealer: enemy, cardSource: null, cardPlay: null);
        AutoTestAssert.Equal(playerHpBefore - 7, player.Creature.CurrentHp, "Unpowered damage bypasses RuggedPower");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<RuggedPower>(), "RuggedPower does not decrement on unpowered damage");
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 15m, ValueProp.Move, dealer: enemy, cardSource: null, cardPlay: null);
        AutoTestAssert.Equal(playerHpBefore - 9, player.Creature.CurrentHp, "RuggedPower capped 15 attack damage to 2");
        AutoTestAssert.True(!player.Creature.HasPower<RuggedPower>(), "RuggedPower decremented to 0 after triggering");

        // 6. BruisePower and unused ConcentratedPower wear off at end of player turn.
        await PowerCmd.Apply<BruisePower>(new ThrowingPlayerChoiceContext(), enemy, 3m, player.Creature, null);
        await PowerCmd.Apply<ConcentratedPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1m, player.Creature, null);
        AutoTestAssert.Equal(3, enemy.GetPowerAmount<BruisePower>(), "BruisePower applied before turn end");
        AutoTestAssert.True(player.Creature.HasPower<ConcentratedPower>(), "ConcentratedPower applied before turn end");
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<BruisePower>(), "BruisePower removed after player turn end");
        AutoTestAssert.True(!player.Creature.HasPower<ConcentratedPower>(), "Unused ConcentratedPower expired after player turn end");
    }
}
