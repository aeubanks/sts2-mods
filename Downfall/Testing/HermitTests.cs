using Downfall.Hermit;
using Downfall.Hermit.Cards;
using Downfall.Hermit.Potions;
using Downfall.Hermit.Powers;
using Downfall.Hermit.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
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

        // Old Locket adds MementoCard to leftmost slot of hand before Turn 1 draw (1 Memento + 5 drawn = 6).
        var memento = AutoTestAssert.NotNull(
            pcs.Hand.Cards.OfType<MementoCard>().FirstOrDefault(),
            "OldLocket added MementoCard to Turn 1 hand");
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "Turn 1 hand has 5 drawn cards + 1 MementoCard");
        AutoTestAssert.True(ReferenceEquals(pcs.Hand.Cards[0], memento), "MementoCard starts on the leftmost side of Hand");

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

    public static async Task TestCommonCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHermit>("DFHRMCOM");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");

        await CreatureCmd.GainMaxHp(enemy, 500m);

        async Task ResetHandAndEnergy()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy < 10)
                await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
        }

        // 1. BodyArmor: discarding Skill gives double Block (10); discarding Attack gives single Block (5).
        await ResetHandAndEnergy();
        var bodyArmor1 = combatState.CreateCard<BodyArmor>(player);
        var skillToDiscard = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(bodyArmor1, PileType.Hand);
        await CardPileCmd.Add(skillToDiscard, PileType.Hand);
        int blockBefore = player.Creature.Block;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, skillToDiscard)))
        {
            AutoTestAssert.True(bodyArmor1.TryManualPlay(null), "Play BodyArmor discarding Skill");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(blockBefore + 10, player.Creature.Block, "BodyArmor discarding non-Attack grants 10 Block");

        await ResetHandAndEnergy();
        var bodyArmor2 = combatState.CreateCard<BodyArmor>(player);
        var attackToDiscard = combatState.CreateCard<HermitStrike>(player);
        await CardPileCmd.Add(bodyArmor2, PileType.Hand);
        await CardPileCmd.Add(attackToDiscard, PileType.Hand);
        blockBefore = player.Creature.Block;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, attackToDiscard)))
        {
            AutoTestAssert.True(bodyArmor2.TryManualPlay(null), "Play BodyArmor discarding Attack");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(blockBefore + 5, player.Creature.Block, "BodyArmor discarding Attack grants 5 Block");

        // 2. Dive (Dead On): gains 8 Block + 1 PlatingPower.
        await ResetHandAndEnergy();
        var dive = combatState.CreateCard<Dive>(player);
        await CardPileCmd.Add(dive, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(dive.TryManualPlay(null), "Play Dead On Dive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 8, player.Creature.Block, "Dive gained 8 Block");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<PlatingPower>(), "Dead On Dive gained 1 PlatingPower");

        // 3. CalledShot right after Dead On Dive: deals 5 damage AND draws 1 card;
        //    a second CalledShot (after non-Dead On CalledShot) deals 5 damage and draws 0 cards!
        var calledShot = combatState.CreateCard<CalledShot>(player);
        await CardPileCmd.Add(calledShot, PileType.Hand);
        int hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(calledShot.TryManualPlay(enemy), "Play CalledShot after Dead On");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 5, enemy.CurrentHp, "CalledShot dealt 5 damage");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "CalledShot drew 1 card after Dead On");

        await ResetHandAndEnergy();
        var calledShot2 = combatState.CreateCard<CalledShot>(player);
        await CardPileCmd.Add(calledShot2, PileType.Hand);
        AutoTestAssert.True(calledShot2.TryManualPlay(enemy), "Play CalledShot after non-Dead On card");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, pcs.Hand.Cards.Count, "CalledShot did not draw after non-Dead On card");

        // 4. Feint: gains 3 Block + applies 2 Bruise to enemy.
        await ResetHandAndEnergy();
        var feint = combatState.CreateCard<Feint>(player);
        await CardPileCmd.Add(feint, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(feint.TryManualPlay(null), "Play Feint");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 3, player.Creature.Block, "Feint gained 3 Block");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<BruisePower>(), "Feint applied 2 Bruise");
        await PowerCmd.Remove<BruisePower>(enemy);

        // 5. Glare: applies 1 Weak and 1 Vulnerable.
        await ResetHandAndEnergy();
        var glare = combatState.CreateCard<Glare>(player);
        await CardPileCmd.Add(glare, PileType.Hand);
        AutoTestAssert.True(glare.TryManualPlay(enemy), "Play Glare");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "Glare applied 1 Weak");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "Glare applied 1 Vulnerable");
        await PowerCmd.Remove<WeakPower>(enemy);
        await PowerCmd.Remove<VulnerablePower>(enemy);

        // 6. Headshot (Dead On): deals 14 damage (2x 7).
        await ResetHandAndEnergy();
        var headshot = combatState.CreateCard<Headshot>(player);
        await CardPileCmd.Add(headshot, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(headshot.TryManualPlay(enemy), "Play Dead On Headshot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 14, enemy.CurrentHp, "Dead On Headshot dealt double (14) damage");

        // 7. HighCaliber: deals 6 damage, adds 12-damage Strike+ to hand, exhausts.
        await ResetHandAndEnergy();
        var highCal = combatState.CreateCard<HighCaliber>(player);
        await CardPileCmd.Add(highCal, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(highCal.TryManualPlay(enemy), "Play HighCaliber");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 6, enemy.CurrentHp, "HighCaliber dealt 6 damage");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(highCal), "HighCaliber exhausted");
        var boostedStrike = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<HermitStrike>().FirstOrDefault(), "HighCaliber added HermitStrike to hand");
        AutoTestAssert.Equal(12m, boostedStrike.DynamicVars.Damage.BaseValue, "HighCaliber Strike has 12 base damage");

        // 8. HoleUp: gains 12 Block and 2 Weak.
        await ResetHandAndEnergy();
        var holeUp = combatState.CreateCard<HoleUp>(player);
        await CardPileCmd.Add(holeUp, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(holeUp.TryManualPlay(null), "Play HoleUp");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 12, player.Creature.Block, "HoleUp gained 12 Block");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<WeakPower>(), "HoleUp applied 2 Weak to player");

        // 9. LowProfile with 1 debuff (Weak) + 2 Dexterity: gains 7 + 4 + 2 = 13 Block (Dexterity applied once, not twice).
        await ResetHandAndEnergy();
        await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), player.Creature, 2m, player.Creature, null);
        var lowProfile = combatState.CreateCard<LowProfile>(player);
        await CardPileCmd.Add(lowProfile, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(lowProfile.TryManualPlay(null), "Play LowProfile with 1 debuff + 2 Dex");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 13, player.Creature.Block, "LowProfile gained 7 + 4 + 2 Dex = 13 Block");
        await PowerCmd.Remove<WeakPower>(player.Creature);
        await PowerCmd.Remove<DexterityPower>(player.Creature);

        // 10. ItchyTrigger (Dead On, middle of 3 cards): deals 7 damage and reduces a hand card's cost by 1.
        await ResetHandAndEnergy();
        var costTarget1 = combatState.CreateCard<Manifest>(player);
        var itchy = combatState.CreateCard<ItchyTrigger>(player);
        var costTarget2 = combatState.CreateCard<Manifest>(player);
        await CardPileCmd.Add(costTarget1, PileType.Hand);
        await CardPileCmd.Add(itchy, PileType.Hand);
        await CardPileCmd.Add(costTarget2, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(itchy.TryManualPlay(enemy), "Play Dead On ItchyTrigger");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 7, enemy.CurrentHp, "ItchyTrigger dealt 7 damage");
        AutoTestAssert.True(
            costTarget1.EnergyCost.GetWithModifiers(CostModifiers.All) == 1
            || costTarget2.EnergyCost.GetWithModifiers(CostModifiers.All) == 1,
            "Dead On ItchyTrigger reduced a hand card's cost by 1");

        // 11. Manifest: gains 16 Block and adds Decay to hand.
        await ResetHandAndEnergy();
        var manifest = combatState.CreateCard<Manifest>(player);
        await CardPileCmd.Add(manifest, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(manifest.TryManualPlay(null), "Play Manifest");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 16, player.Creature.Block, "Manifest gained 16 Block");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is StsCards.Decay), "Manifest added Decay to hand");

        // 12. Misfire: deals 11 damage and shuffles Clumsy into draw pile.
        await ResetHandAndEnergy();
        var misfire = combatState.CreateCard<Misfire>(player);
        await CardPileCmd.Add(misfire, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(misfire.TryManualPlay(enemy), "Play Misfire");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 11, enemy.CurrentHp, "Misfire dealt 11 damage");
        AutoTestAssert.True(PileType.Draw.GetPile(player).Cards.Any(c => c is StsCards.Clumsy), "Misfire shuffled Clumsy into draw pile");

        // 13. PistolWhip: deals 6 damage and applies 3 Bruise.
        await ResetHandAndEnergy();
        var pistolWhip = combatState.CreateCard<PistolWhip>(player);
        await CardPileCmd.Add(pistolWhip, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(pistolWhip.TryManualPlay(enemy), "Play PistolWhip");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 6, enemy.CurrentHp, "PistolWhip dealt 6 damage");
        AutoTestAssert.Equal(3, enemy.GetPowerAmount<BruisePower>(), "PistolWhip applied 3 Bruise");
        await PowerCmd.Remove<BruisePower>(enemy);

        // 14. Roulette: deals 16 damage, discards hand (2 cards), draws 2 cards.
        await ResetHandAndEnergy();
        var roulette = combatState.CreateCard<Roulette>(player);
        var h1 = combatState.CreateCard<HermitStrike>(player);
        var h2 = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(roulette, PileType.Hand);
        await CardPileCmd.Add(h1, PileType.Hand);
        await CardPileCmd.Add(h2, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(roulette.TryManualPlay(enemy), "Play Roulette");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 16, enemy.CurrentHp, "Roulette dealt 16 damage");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Roulette replaced 2 discarded hand cards with 2 drawn cards");

        // 15. SprayPray: deals 4x3 = 12 damage and shuffles Doubt into draw pile.
        await ResetHandAndEnergy();
        var sprayPray = combatState.CreateCard<SprayPray>(player);
        await CardPileCmd.Add(sprayPray, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(sprayPray.TryManualPlay(null), "Play SprayPray");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 12, enemy.CurrentHp, "SprayPray dealt 12 total damage");
        AutoTestAssert.True(PileType.Draw.GetPile(player).Cards.Any(c => c is StsCards.Doubt), "SprayPray shuffled Doubt into draw pile");

        // 16. TrackingShot: applies ConcentratedPower and deals 4x2 = 8 damage.
        await ResetHandAndEnergy();
        var tracking = combatState.CreateCard<TrackingShot>(player);
        await CardPileCmd.Add(tracking, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(tracking.TryManualPlay(enemy), "Play TrackingShot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 8, enemy.CurrentHp, "TrackingShot dealt 8 damage");
        AutoTestAssert.True(player.Creature.HasPower<ConcentratedPower>(), "TrackingShot applied ConcentratedPower");

        // 17. Vantage (off-center but Concentrated!): gains 8 Block, draws 1 card and upgrades it, consumes Concentration!
        var topUnupgraded = combatState.CreateCard<HermitStrike>(player);
        await CardPileCmd.Add(topUnupgraded, PileType.Draw, CardPilePosition.Top);
        var vantage = combatState.CreateCard<Vantage>(player);
        var vPad1 = combatState.CreateCard<HermitDefend>(player);
        var vPad2 = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(vantage, PileType.Hand);
        await CardPileCmd.Add(vPad1, PileType.Hand);
        await CardPileCmd.Add(vPad2, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(vantage.TryManualPlay(null), "Play Concentrated Vantage");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 8, player.Creature.Block, "Vantage gained 8 Block");
        AutoTestAssert.True(topUnupgraded.IsUpgraded, "Dead On Vantage upgraded the drawn card");
        AutoTestAssert.True(!player.Creature.HasPower<ConcentratedPower>(), "Vantage consumed ConcentratedPower");

        // 18. WideOpen: deals 6 damage and applies 1 Vulnerable.
        await ResetHandAndEnergy();
        var wideOpen = combatState.CreateCard<WideOpen>(player);
        await CardPileCmd.Add(wideOpen, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(wideOpen.TryManualPlay(enemy), "Play WideOpen");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 6, enemy.CurrentHp, "WideOpen dealt 6 damage");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "WideOpen applied 1 Vulnerable");
        await PowerCmd.Remove<VulnerablePower>(enemy);

        // 19 & 20. Quickdraw + Coalescence across turn end:
        // Quickdraw deals 9, draws 2, applies HermitDrawLessNextTurnPower(1).
        // Coalescence gains 6 Block and retains 1 chosen card across turn end!
        await ResetHandAndEnergy();
        var quickdraw = combatState.CreateCard<Quickdraw>(player);
        await CardPileCmd.Add(quickdraw, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(quickdraw.TryManualPlay(enemy), "Play Quickdraw");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 9, enemy.CurrentHp, "Quickdraw dealt 9 damage");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Quickdraw drew 2 cards");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<HermitDrawLessNextTurnPower>(), "Quickdraw applied DrawLess(1)");

        var retainTarget = combatState.CreateCard<HermitStrike>(player);
        var coalescence = combatState.CreateCard<Coalescence>(player);
        await CardPileCmd.Add(retainTarget, PileType.Hand);
        await CardPileCmd.Add(coalescence, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(coalescence.TryManualPlay(null), "Play Coalescence");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 6, player.Creature.Block, "Coalescence gained 6 Block");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<CoalescencePower>(), "Coalescence applied CoalescencePower(2)");

        int autoRetainedBeforeEnd = pcs.Hand.Cards.Count(c => c.ShouldRetainThisTurn);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, retainTarget)))
        {
            await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        }
        AutoTestAssert.True(pcs.Hand.Cards.Contains(retainTarget), "CoalescencePower retained chosen card into Turn 2");
        AutoTestAssert.True(!player.Creature.HasPower<CoalescencePower>(), "CoalescencePower removed after turn end");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<HermitDrawLessNextTurnPower>(), "HermitDrawLessNextTurnPower consumed on Turn 2 draw");
        // Exact Turn 2 hand size: any naturally retained cards + 1 retained via Coalescence + 4 drawn (5 - 1 from Quickdraw).
        AutoTestAssert.Equal(autoRetainedBeforeEnd + 1 + 4, pcs.Hand.Cards.Count, "Turn 2 hand equals retained cards + 4 drawn (DrawLess -1)");
    }

    public static async Task TestUncommonBatch1InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHermit>("DFHRMU1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");

        await CreatureCmd.GainMaxHp(enemy, 600m);

        async Task ResetHandAndEnergy()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy < 10)
                await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
        }

        // 1. BigShot (Smoking Barrel) + Combo + Deadeye (Dead On):
        //    Deadeye triggers Dead On -> +2 Str, Combo returns Deadeye to Hand (first Dead On this turn), BigShot grants 3 Vigor!
        //    Playing Deadeye a 2nd time -> consumes 3 Vigor, goes to Discard (Combo limit 1 used up).
        await ResetHandAndEnergy();
        var bigShot = combatState.CreateCard<BigShot>(player);
        var combo = combatState.CreateCard<Combo>(player);
        await CardPileCmd.Add(bigShot, PileType.Hand);
        await CardPileCmd.Add(combo, PileType.Hand);
        AutoTestAssert.True(bigShot.TryManualPlay(null), "Play BigShot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(combo.TryManualPlay(null), "Play Combo");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        var deadeye = combatState.CreateCard<Deadeye>(player);
        await CardPileCmd.Add(deadeye, PileType.Hand);
        int hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(deadeye.TryManualPlay(enemy), "Play Dead On Deadeye #1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 5, enemy.CurrentHp, "Deadeye #1 dealt 5 damage");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Dead On Deadeye gained 2 Strength");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<VigorPower>(), "BigShotPower granted 3 Vigor after Dead On");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(deadeye), "ComboPower returned 1st Dead On card to Hand");

        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(deadeye.TryManualPlay(enemy), "Play Dead On Deadeye #2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - (5 + 2 + 3), enemy.CurrentHp, "Deadeye #2 dealt 5 + 2 Str + 3 Vigor = 10 damage");
        AutoTestAssert.True(PileType.Discard.GetPile(player).Cards.Contains(deadeye), "2nd Dead On play went to Discard (Combo limit 1)");
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<VigorPower>(player.Creature);
        await PowerCmd.Remove<BigShotPower>(player.Creature);
        await PowerCmd.Remove<ComboPower>(player.Creature);

        // 2. Cheat (Dead On): plays GhostlyPresence from top of draw pile AND forces its Dead On effect (8 Block + 1 Weak to enemy)!
        await ResetHandAndEnergy();
        var ghostly = combatState.CreateCard<GhostlyPresence>(player);
        await CardPileCmd.Add(ghostly, PileType.Draw, CardPilePosition.Top);
        var cheat = combatState.CreateCard<Cheat>(player);
        await CardPileCmd.Add(cheat, PileType.Hand);
        int blockBefore = player.Creature.Block;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, ghostly)))
        {
            AutoTestAssert.True(cheat.TryManualPlay(null), "Play Dead On Cheat");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(blockBefore + 8, player.Creature.Block, "Cheated GhostlyPresence gained 8 Block");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "Dead On Cheat forced GhostlyPresence Dead On (1 Weak)");
        await PowerCmd.Remove<WeakPower>(enemy);

        // 2b. Non-Dead-On Cheat does NOT force cheated card's Dead On, and Cheat -> TrackingShot keeps Concentration!
        await ResetHandAndEnergy();
        var ghostly2 = combatState.CreateCard<GhostlyPresence>(player);
        await CardPileCmd.Add(ghostly2, PileType.Draw, CardPilePosition.Top);
        var cheatNonDeadOn = combatState.CreateCard<Cheat>(player);
        await CardPileCmd.Add(cheatNonDeadOn, PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<HermitDefend>(player), PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<HermitDefend>(player), PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, ghostly2)))
        {
            AutoTestAssert.True(cheatNonDeadOn.TryManualPlay(null), "Play non-Dead-On Cheat");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<WeakPower>(), "Non-Dead-On Cheat did NOT force GhostlyPresence Dead On");

        await ResetHandAndEnergy();
        var tracking = combatState.CreateCard<TrackingShot>(player);
        await CardPileCmd.Add(tracking, PileType.Draw, CardPilePosition.Top);
        var cheatTrack = combatState.CreateCard<Cheat>(player);
        await CardPileCmd.Add(cheatTrack, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, tracking)))
        {
            AutoTestAssert.True(cheatTrack.TryManualPlay(null), "Play Cheat -> TrackingShot");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.True(player.Creature.HasPower<ConcentratedPower>(), "Cheat -> TrackingShot preserved newly applied ConcentratedPower");
        await PowerCmd.Remove<ConcentratedPower>(player.Creature);

        // 3. CursedWeapon: loses 2 HP, deals 10 damage, exhausts, permanently increases CursedWeapon BonusDamage in combat + master deck!
        await ResetHandAndEnergy();
        var cwDeck = player.RunState.CreateCard<CursedWeapon>(player);
        await CardPileCmd.Add(cwDeck, PileType.Deck);
        var cw1 = combatState.CreateCard<CursedWeapon>(player);
        var cw2 = combatState.CreateCard<CursedWeapon>(player);
        await CardPileCmd.Add(cw1, PileType.Hand);
        await CardPileCmd.Add(cw2, PileType.Discard);
        int playerHpBefore = player.Creature.CurrentHp;
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(cw1.TryManualPlay(enemy), "Play CursedWeapon");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(playerHpBefore - 2, player.Creature.CurrentHp, "CursedWeapon lost 2 HP");
        AutoTestAssert.Equal(hpBefore - 10, enemy.CurrentHp, "CursedWeapon dealt 10 damage");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(cw1), "CursedWeapon exhausted");
        AutoTestAssert.Equal(1, cw1.BonusDamage, "CursedWeapon self BonusDamage increased to 1");
        AutoTestAssert.Equal(11m, cw2.DynamicVars.Damage.BaseValue, "Other CursedWeapon in combat increased to 11 damage");
        AutoTestAssert.Equal(1, cwDeck.BonusDamage, "Master-deck CursedWeapon BonusDamage increased to 1");

        // 4. DeadMansHand: discards hand and fetches 3 rarest cards from draw pile!
        await ResetHandAndEnergy();
        foreach (var c in PileType.Draw.GetPile(player).Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var dmh = combatState.CreateCard<DeadMansHand>(player);
        var handFiller = combatState.CreateCard<HermitDefend>(player);
        var uncommonDraw1 = combatState.CreateCard<Deadeye>(player);
        var uncommonDraw2 = combatState.CreateCard<Brawl>(player);
        var commonDraw = combatState.CreateCard<Headshot>(player);
        var basicDraw = combatState.CreateCard<HermitStrike>(player);
        await CardPileCmd.Add(dmh, PileType.Hand);
        await CardPileCmd.Add(handFiller, PileType.Hand);
        await CardPileCmd.Add(basicDraw, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(commonDraw, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(uncommonDraw1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(uncommonDraw2, PileType.Draw, CardPilePosition.Top);
        AutoTestAssert.True(dmh.TryManualPlay(null), "Play DeadMansHand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "DeadMansHand fetched 3 rarest cards");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(uncommonDraw1) && pcs.Hand.Cards.Contains(uncommonDraw2) && pcs.Hand.Cards.Contains(commonDraw),
            "DeadMansHand prioritized Uncommon & Common over Basic");

        // 5. Desperado: deals 10, doubles to 20 and increases cost 1 -> 2.
        await ResetHandAndEnergy();
        var desperado = combatState.CreateCard<Desperado>(player);
        await CardPileCmd.Add(desperado, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(desperado.TryManualPlay(enemy), "Play Desperado");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 10, enemy.CurrentHp, "Desperado dealt 10 damage");
        AutoTestAssert.Equal(20m, desperado.DynamicVars.Damage.BaseValue, "Desperado doubled damage to 20");
        AutoTestAssert.Equal(2, desperado.EnergyCost.GetWithModifiers(CostModifiers.All), "Desperado cost increased to 2");

        // 6. Determination + Gestalt: Gestalt grants 2 Rugged + 2 Vulnerable -> Determination triggers +1 Strength!
        //    Decrementing Rugged or decaying Vulnerable must NOT grant Strength; negative Strength DOES grant Strength!
        await ResetHandAndEnergy();
        var determination = combatState.CreateCard<Determination>(player);
        var gestalt = combatState.CreateCard<Gestalt>(player);
        await CardPileCmd.Add(determination, PileType.Hand);
        await CardPileCmd.Add(gestalt, PileType.Hand);
        AutoTestAssert.True(determination.TryManualPlay(null), "Play Determination");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(gestalt.TryManualPlay(null), "Play Gestalt");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<RuggedPower>(), "Gestalt granted 2 Rugged");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<VulnerablePower>(), "Gestalt applied 2 Vulnerable");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "Determination granted 1 Strength on Vulnerable debuff");
        // Decrementing a buff (Rugged) and decaying a debuff (Vulnerable -1) must NOT trigger Determination:
        var ctx = new ThrowingPlayerChoiceContext();
        await PowerCmd.ModifyAmount(ctx, player.Creature.GetPower<RuggedPower>()!, -1m, null, null);
        await PowerCmd.ModifyAmount(ctx, player.Creature.GetPower<VulnerablePower>()!, -1m, null, null);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "Determination did NOT trigger on buff decrement or debuff decay");
        // Negative Strength IS a debuff application -> Determination grants +1 Strength (net change: -1 + 1 = 0):
        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, -1m, enemy, null);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "Determination triggered +1 Strength on negative Strength debuff");
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<VulnerablePower>(player.Creature);
        await PowerCmd.Remove<RuggedPower>(player.Creature);
        await PowerCmd.Remove<DeterminationPower>(player.Creature);

        // 7. Enervate (Dead On): deals 7 damage, gains 1 Energy, draws 1 card.
        await ResetHandAndEnergy();
        var enervate = combatState.CreateCard<Enervate>(player);
        await CardPileCmd.Add(enervate, PileType.Hand);
        int energyBefore = pcs.Energy;
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(enervate.TryManualPlay(enemy), "Play Dead On Enervate");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 7, enemy.CurrentHp, "Enervate dealt 7 damage");
        AutoTestAssert.Equal(energyBefore, pcs.Energy, "Dead On Enervate refunded 1 energy (spent 1, gained 1)");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "Dead On Enervate drew 1 card");

        // 8. EyeOfTheStorm: Concentrates and refills energy to MaxEnergy (3), exhausts.
        await ResetHandAndEnergy();
        await PlayerCmd.LoseEnergy(pcs.Energy - 1, player);
        var eye = combatState.CreateCard<EyeOfTheStorm>(player);
        await CardPileCmd.Add(eye, PileType.Hand);
        AutoTestAssert.True(eye.TryManualPlay(null), "Play EyeOfTheStorm at 1 energy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(player.Creature.HasPower<ConcentratedPower>(), "EyeOfTheStorm applied ConcentratedPower");
        AutoTestAssert.Equal(player.MaxEnergy, pcs.Energy, "EyeOfTheStorm refilled Energy to MaxEnergy");
        await PowerCmd.Remove<ConcentratedPower>(player.Creature);

        // 9. FlashPowder: gains 5 Block, enemy loses 1 Strength, exhausts.
        await ResetHandAndEnergy();
        var flash = combatState.CreateCard<FlashPowder>(player);
        await CardPileCmd.Add(flash, PileType.Hand);
        blockBefore = player.Creature.Block;
        int enemyStrBefore = enemy.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(flash.TryManualPlay(null), "Play FlashPowder");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 5, player.Creature.Block, "FlashPowder gained 5 Block");
        AutoTestAssert.Equal(enemyStrBefore - 1, enemy.GetPowerAmount<StrengthPower>(), "FlashPowder reduced enemy Strength by 1");

        // 10. FullyLoaded: moves ALL Strikes & Defends from Draw Pile to Hand!
        await ResetHandAndEnergy();
        foreach (var c in PileType.Draw.GetPile(player).Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var drawStrike = combatState.CreateCard<HermitStrike>(player);
        var drawDefend = combatState.CreateCard<HermitDefend>(player);
        var drawOther = combatState.CreateCard<Headshot>(player);
        await CardPileCmd.Add(drawStrike, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(drawDefend, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(drawOther, PileType.Draw, CardPilePosition.Top);
        var loaded = combatState.CreateCard<FullyLoaded>(player);
        await CardPileCmd.Add(loaded, PileType.Hand);
        AutoTestAssert.True(loaded.TryManualPlay(null), "Play FullyLoaded");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.Hand.Cards.Contains(drawStrike) && pcs.Hand.Cards.Contains(drawDefend), "FullyLoaded fetched Strike and Defend");
        AutoTestAssert.True(!pcs.Hand.Cards.Contains(drawOther), "FullyLoaded left non-Strike/Defend in Draw Pile");

        // 11. Grudge: deals 9 + 2 * (non-Exhaust Curses in combat piles) to all enemies.
        await ResetHandAndEnergy();
        int curseCount = pcs.AllCards.Count(c => c.Type == CardType.Curse && c.Pile?.Type != PileType.Exhaust);
        var extraCurse = combatState.CreateCard<StsCards.Clumsy>(player);
        await CardPileCmd.Add(extraCurse, PileType.Discard);
        curseCount++;
        var exhaustedCurse = combatState.CreateCard<StsCards.Clumsy>(player);
        await CardPileCmd.Add(exhaustedCurse, PileType.Exhaust);
        var grudge = combatState.CreateCard<Grudge>(player);
        await CardPileCmd.Add(grudge, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(grudge.TryManualPlay(null), "Play Grudge");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - (9 + 2 * curseCount), enemy.CurrentHp, $"Grudge dealt 9 + 2*{curseCount} damage (excluding Exhausted Curse)");

        // 12. Brawl: applies 3 Bruise to all enemies at start of Turn 2.
        await ResetHandAndEnergy();
        var brawl = combatState.CreateCard<Brawl>(player);
        await CardPileCmd.Add(brawl, PileType.Hand);
        AutoTestAssert.True(brawl.TryManualPlay(null), "Play Brawl");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(3, enemy.GetPowerAmount<BruisePower>(), "BrawlPower applied 3 Bruise on Turn 2 start");

        // 13. GoldenBullet: Fatal permanently reduces CostReduction (3 -> 2)!
        // Spawn a 2nd Nibbit so killing the 1st doesn't end combat prematurely before assertions.
        await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        await ResetHandAndEnergy();
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), enemy, enemy.CurrentHp - 5, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        var bullet = combatState.CreateCard<GoldenBullet>(player);
        await CardPileCmd.Add(bullet, PileType.Hand);
        AutoTestAssert.True(bullet.TryManualPlay(enemy), "Play GoldenBullet for Fatal kill");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(enemy.IsDead, "GoldenBullet killed target");
        AutoTestAssert.Equal(1, bullet.CostReduction, "Fatal GoldenBullet increased CostReduction to 1");
        AutoTestAssert.Equal(2, bullet.EnergyCost.GetWithModifiers(CostModifiers.All), "Fatal GoldenBullet cost permanently reduced 3 -> 2");
    }

    public static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHermit>("DFHRMU2");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);

        await CreatureCmd.GainMaxHp(enemy1, 800m);
        await CreatureCmd.GainMaxHp(enemy2, 800m);

        async Task ResetHandAndEnergy()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy < 10)
                await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
        }

        // 1. Snipe+ + Scavenge (Dead On) + Ricochet:
        //    Snipe+ applies ConcentratedPower + SnipePower(1).
        //    Scavenge gains 4 Plating and triggers Dead On TWICE -> +10 Gold (2 * 5) and records 2 Dead On triggers this turn!
        //    Ricochet deals 7 to enemy1 + 2 random hits (7 each) -> 21 total damage across enemies!
        await ResetHandAndEnergy();
        var snipe = combatState.CreateCard<Snipe>(player);
        CardCmd.Upgrade(snipe);
        var scavenge = combatState.CreateCard<Scavenge>(player);
        var ricochet = combatState.CreateCard<Ricochet>(player);
        await CardPileCmd.Add(snipe, PileType.Hand);
        await CardPileCmd.Add(scavenge, PileType.Hand);
        await CardPileCmd.Add(ricochet, PileType.Hand);
        int goldBefore = player.Gold;
        AutoTestAssert.True(snipe.TryManualPlay(null), "Play Snipe+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(player.Creature.HasPower<ConcentratedPower>(), "Snipe+ applied ConcentratedPower");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<SnipePower>(), "Snipe+ applied 1 SnipePower");

        AutoTestAssert.True(scavenge.TryManualPlay(null), "Play Scavenge with Snipe+Concentration");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<PlatingPower>(), "Scavenge applied 4 Plating");
        AutoTestAssert.Equal(goldBefore + 10, player.Gold, "Sniped Scavenge doubled Dead On Gold (+10)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<SnipePower>(), "SnipePower consumed by Scavenge");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(scavenge), "Scavenge exhausted");

        int totalHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(ricochet.TryManualPlay(enemy1), "Play Ricochet after 2 Dead On triggers");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHpBefore - 21, enemy1.CurrentHp + enemy2.CurrentHp, "Ricochet dealt 7 + 2*7 = 21 total damage");
        await PowerCmd.Remove<PlatingPower>(player.Creature);

        // 2. Snipe + Headshot (Dead On): deals 3x damage (21 instead of 14)!
        //    Also Snipe + Dead On Cheat -> Deadeye exercises ForceDeadOnOnce(2) -> +4 Strength without double-consuming Snipe!
        await ResetHandAndEnergy();
        var snipe2 = combatState.CreateCard<Snipe>(player);
        var headshot = combatState.CreateCard<Headshot>(player);
        await CardPileCmd.Add(snipe2, PileType.Hand);
        await CardPileCmd.Add(headshot, PileType.Hand);
        AutoTestAssert.True(snipe2.TryManualPlay(null), "Play Snipe");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        int hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(headshot.TryManualPlay(enemy1), "Play Sniped Dead On Headshot");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 21, enemy1.CurrentHp, "Sniped Dead On Headshot dealt 3x damage (21)");

        await ResetHandAndEnergy();
        var snipe3 = combatState.CreateCard<Snipe>(player);
        var cheatSniped = combatState.CreateCard<Cheat>(player);
        var cheatedDeadeye = combatState.CreateCard<Deadeye>(player);
        await CardPileCmd.Add(cheatedDeadeye, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(snipe3, PileType.Hand);
        await CardPileCmd.Add(cheatSniped, PileType.Hand);
        AutoTestAssert.True(snipe3.TryManualPlay(null), "Play Snipe for Cheat");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, cheatedDeadeye)))
        {
            AutoTestAssert.True(cheatSniped.TryManualPlay(null), "Play Sniped Dead On Cheat -> Deadeye");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<StrengthPower>(), "Sniped Cheat -> Deadeye triggered Dead On twice (+4 Str)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<SnipePower>(), "SnipePower cleanly consumed once");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        // 3. LoneWolf: keeps preferred card at cost 0 this turn, discards the rest of hand.
        await ResetHandAndEnergy();
        var loneWolf = combatState.CreateCard<LoneWolf>(player);
        var keepCard = combatState.CreateCard<Malice>(player);
        var discardA = combatState.CreateCard<HermitDefend>(player);
        var discardB = combatState.CreateCard<HermitStrike>(player);
        await CardPileCmd.Add(loneWolf, PileType.Hand);
        await CardPileCmd.Add(keepCard, PileType.Hand);
        await CardPileCmd.Add(discardA, PileType.Hand);
        await CardPileCmd.Add(discardB, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, keepCard)))
        {
            AutoTestAssert.True(loneWolf.TryManualPlay(null), "Play LoneWolf");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "LoneWolf discarded rest of hand");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(keepCard), "LoneWolf kept selected card");
        AutoTestAssert.Equal(0, keepCard.EnergyCost.GetWithModifiers(CostModifiers.All), "LoneWolf set kept card cost to 0");

        // 4. LuckOfTheDraw: draws until total cost >= 3 (0 + 1 + 2 = 3 -> stops before 4th card!).
        await ResetHandAndEnergy();
        foreach (var c in PileType.Draw.GetPile(player).Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var extra4 = combatState.CreateCard<HermitStrike>(player); // cost 1 (should NOT be drawn)
        var card2 = combatState.CreateCard<Malice>(player);        // cost 2
        var card1 = combatState.CreateCard<HermitStrike>(player);  // cost 1
        var card0 = combatState.CreateCard<Covet>(player);         // cost 0
        await CardPileCmd.Add(extra4, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(card2, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(card1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(card0, PileType.Draw, CardPilePosition.Top);
        var luck = combatState.CreateCard<LuckOfTheDraw>(player);
        await CardPileCmd.Add(luck, PileType.Hand);
        AutoTestAssert.True(luck.TryManualPlay(null), "Play LuckOfTheDraw");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "LuckOfTheDraw drew exactly 3 cards (costs 0+1+2 = 3)");
        AutoTestAssert.True(!pcs.Hand.Cards.Contains(extra4), "LuckOfTheDraw stopped once total cost reached 3");

        // 5. Maintenance: +3 Strike damage, +1 Dexterity, cost increases 1 -> 2 this combat.
        await ResetHandAndEnergy();
        var maint = combatState.CreateCard<Maintenance>(player);
        var maintStrike = combatState.CreateCard<HermitStrike>(player);
        await CardPileCmd.Add(maint, PileType.Hand);
        await CardPileCmd.Add(maintStrike, PileType.Hand);
        AutoTestAssert.True(maint.TryManualPlay(null), "Play Maintenance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<MaintenanceStrikePower>(), "Maintenance applied 3 MaintenanceStrikePower");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DexterityPower>(), "Maintenance applied 1 Dexterity");
        AutoTestAssert.Equal(2, maint.EnergyCost.GetWithModifiers(CostModifiers.All), "Maintenance cost increased 1 -> 2 this combat");
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(maintStrike.TryManualPlay(enemy1), "Play Strike buffed by Maintenance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 9, enemy1.CurrentHp, "Maintenance-buffed Strike dealt 9 damage (6+3)");
        await PowerCmd.Remove<MaintenanceStrikePower>(player.Creature);
        await PowerCmd.Remove<DexterityPower>(player.Creature);

        // 6. Malice: exhausting non-Curse deals 16 single-target; exhausting Curse deals 16 AOE!
        await ResetHandAndEnergy();
        var maliceSingle = combatState.CreateCard<Malice>(player);
        var nonCurseFodder = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(maliceSingle, PileType.Hand);
        await CardPileCmd.Add(nonCurseFodder, PileType.Hand);
        int e1Before = enemy1.CurrentHp;
        int e2Before = enemy2.CurrentHp;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, nonCurseFodder)))
        {
            AutoTestAssert.True(maliceSingle.TryManualPlay(enemy1), "Play Malice exhausting non-Curse");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(e1Before - 16, enemy1.CurrentHp, "Malice (non-Curse) dealt 16 to target");
        AutoTestAssert.Equal(e2Before, enemy2.CurrentHp, "Malice (non-Curse) did not hit other enemy");

        await ResetHandAndEnergy();
        var maliceCurse = combatState.CreateCard<Malice>(player);
        var curseFodder = combatState.CreateCard<StsCards.Clumsy>(player);
        await CardPileCmd.Add(maliceCurse, PileType.Hand);
        await CardPileCmd.Add(curseFodder, PileType.Hand);
        e1Before = enemy1.CurrentHp;
        e2Before = enemy2.CurrentHp;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, curseFodder)))
        {
            AutoTestAssert.True(maliceCurse.TryManualPlay(enemy1), "Play Malice exhausting Curse");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(e1Before - 16, enemy1.CurrentHp, "Malice (Curse) dealt 16 to enemy1");
        AutoTestAssert.Equal(e2Before - 16, enemy2.CurrentHp, "Malice (Curse) dealt 16 to enemy2");

        // 7. ShadowCloak + Spite: drawing a Curse grants 4 Block; Spite exhausts Unplayable Curses (+4 Block each), +8 Block, draws 3!
        await ResetHandAndEnergy();
        var shadowCloak = combatState.CreateCard<ShadowCloak>(player);
        await CardPileCmd.Add(shadowCloak, PileType.Hand);
        AutoTestAssert.True(shadowCloak.TryManualPlay(null), "Play ShadowCloak");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        int blockBefore = player.Creature.Block;
        var drawnCurse = combatState.CreateCard<StsCards.Clumsy>(player);
        await CardPileCmd.Add(drawnCurse, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, player);
        AutoTestAssert.Equal(blockBefore + 4, player.Creature.Block, "ShadowCloak gained 4 Block on drawing Curse");

        blockBefore = player.Creature.Block;
        var spite = combatState.CreateCard<Spite>(player);
        await CardPileCmd.Add(spite, PileType.Hand);
        int handBeforeSpite = pcs.Hand.Cards.Count;
        AutoTestAssert.True(spite.TryManualPlay(null), "Play Spite");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(drawnCurse), "Spite exhausted Unplayable Curse");
        AutoTestAssert.Equal(blockBefore + 12, player.Creature.Block, "Spite gained 8 Block + ShadowCloak 4 Block on Curse exhaust");
        AutoTestAssert.Equal(handBeforeSpite - 2 + 3, pcs.Hand.Cards.Count, "Spite drew 3 cards");
        await PowerCmd.Remove<ShadowCloakPower>(player.Creature);

        // 8. Shortfuse: cost 3 -> reduced by 1 per Strike/Defend played this turn -> cost 1!
        await ResetHandAndEnergy();
        var sf = combatState.CreateCard<Shortfuse>(player);
        var sfStrike = combatState.CreateCard<HermitStrike>(player);
        var sfDefend = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(sf, PileType.Hand);
        await CardPileCmd.Add(sfStrike, PileType.Hand);
        await CardPileCmd.Add(sfDefend, PileType.Hand);
        int sfCostBefore = sf.EnergyCost.GetWithModifiers(CostModifiers.All);
        AutoTestAssert.True(sfStrike.TryManualPlay(enemy1), "Play Strike for Shortfuse discount");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(sfDefend.TryManualPlay(null), "Play Defend for Shortfuse discount");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(Math.Max(0, sfCostBefore - 2), sf.EnergyCost.GetWithModifiers(CostModifiers.All), "Shortfuse discounted by 2");
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(sf.TryManualPlay(enemy1), "Play discounted Shortfuse");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 18, enemy1.CurrentHp, "Shortfuse dealt 18 damage");

        // 9. Showdown: deals 9 damage + auto-plays all Strikes in hand!
        await ResetHandAndEnergy();
        var showdown = combatState.CreateCard<Showdown>(player);
        var sdStrike1 = combatState.CreateCard<HermitStrike>(player);
        var sdStrike2 = combatState.CreateCard<HermitStrike>(player);
        await CardPileCmd.Add(showdown, PileType.Hand);
        await CardPileCmd.Add(sdStrike1, PileType.Hand);
        await CardPileCmd.Add(sdStrike2, PileType.Hand);
        totalHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(showdown.TryManualPlay(enemy1), "Play Showdown");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHpBefore - 21, enemy1.CurrentHp + enemy2.CurrentHp, "Showdown dealt 9 + 6 + 6 = 21 total damage");
        AutoTestAssert.Equal(0, pcs.Hand.Cards.Count, "Showdown played both Strikes from hand");

        // 10. TakeCover: X-cost at 2 energy -> adds 0-cost Defend upgraded twice (5 + 6 = 11 Block)!
        await ResetHandAndEnergy();
        await PlayerCmd.SetEnergy(2, player);
        var takeCover = combatState.CreateCard<TakeCover>(player);
        await CardPileCmd.Add(takeCover, PileType.Hand);
        AutoTestAssert.True(takeCover.TryManualPlay(null), "Play TakeCover at X=2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        var generatedDefend = pcs.Hand.Cards.OfType<HermitDefend>().FirstOrDefault();
        AutoTestAssert.NotNull(generatedDefend, "TakeCover added HermitDefend to hand");
        AutoTestAssert.Equal(0, generatedDefend!.EnergyCost.GetWithModifiers(CostModifiers.All), "TakeCover Defend costs 0");
        AutoTestAssert.Equal(11m, generatedDefend.DynamicVars.Block.BaseValue, "TakeCover X=2 upgraded Defend to 11 Block");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(takeCover), "TakeCover exhausted");

        // 11. Virtue: Retain, reduces Weak (3->2), negative Strength (-2->-1), removes 1 Vulnerable!
        await ResetHandAndEnergy();
        var ctx = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<WeakPower>(ctx, player.Creature, 3m, enemy1, null);
        await PowerCmd.Apply<VulnerablePower>(ctx, player.Creature, 1m, enemy1, null);
        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, -2m, enemy1, null);
        var virtue = combatState.CreateCard<Virtue>(player);
        await CardPileCmd.Add(virtue, PileType.Hand);
        AutoTestAssert.True(virtue.TryManualPlay(null), "Play Virtue");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<WeakPower>(), "Virtue reduced Weak 3 -> 2");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<VulnerablePower>(), "Virtue removed 1 Vulnerable");
        AutoTestAssert.Equal(-1, player.Creature.GetPowerAmount<StrengthPower>(), "Virtue reduced negative Strength -2 -> -1");
        await PowerCmd.Remove<WeakPower>(player.Creature);
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        // 12. Midnight (+ImpendingDoom) + Horror + NoHoldsBarred + TakeAim across turn transitions!
        await ResetHandAndEnergy();
        var horror = combatState.CreateCard<Horror>(player);
        var nhb = combatState.CreateCard<NoHoldsBarred>(player);
        var takeAim = combatState.CreateCard<TakeAim>(player);
        var midnight = combatState.CreateCard<Midnight>(player);
        await CardPileCmd.Add(horror, PileType.Hand);
        await CardPileCmd.Add(nhb, PileType.Hand);
        await CardPileCmd.Add(takeAim, PileType.Hand);
        await CardPileCmd.Add(midnight, PileType.Hand);

        AutoTestAssert.True(horror.TryManualPlay(null), "Play Horror");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        e1Before = enemy1.CurrentHp;
        e2Before = enemy2.CurrentHp;
        AutoTestAssert.True(nhb.TryManualPlay(null), "Play NoHoldsBarred");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        // Horror already applied 3 Bruise -> NoHoldsBarred deals 19 + 3 = 22 AOE damage!
        AutoTestAssert.Equal(e1Before - 22, enemy1.CurrentHp, "NoHoldsBarred dealt 19 (+3 Bruise) = 22 to enemy1");
        AutoTestAssert.Equal(e2Before - 22, enemy2.CurrentHp, "NoHoldsBarred dealt 19 (+3 Bruise) = 22 to enemy2");
        AutoTestAssert.True(takeAim.TryManualPlay(null), "Play TakeAim");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        // Clear any leftover Block so Midnight's 12 Block vs ImpendingDoom's 13 self-damage is exact!
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        AutoTestAssert.True(midnight.TryManualPlay(null), "Play Midnight");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, player.Creature.Block, "Midnight gained 12 Block");

        // ImpendingDoom is the ONLY card in hand -> guaranteed Dead On at end of Turn 1!
        var doom = pcs.Hand.Cards.OfType<ImpendingDoom>().FirstOrDefault();
        AutoTestAssert.NotNull(doom, "Midnight added ImpendingDoom to hand");
        AutoTestAssert.Equal(8, enemy1.GetPowerAmount<BruisePower>(), "Horror(3) + NoHoldsBarred(5) = 8 Bruise on enemy1");
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<HorrorPower>(), "Horror applied 1 HorrorPower");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<HermitDrainedPower>(), "NoHoldsBarred applied 1 HermitDrainedPower");

        int playerHpBeforeDoom = player.Creature.CurrentHp;
        e1Before = enemy1.CurrentHp;
        e2Before = enemy2.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);

        // ImpendingDoom fired 13 blockable damage to player (12 blocked -> at least 1 HP lost) and 13 to both enemies!
        AutoTestAssert.True(player.Creature.CurrentHp <= playerHpBeforeDoom - 1, "Dead On ImpendingDoom pierced Midnight's 12 Block for >=1 HP");
        AutoTestAssert.Equal(e1Before - 13, enemy1.CurrentHp, "Dead On ImpendingDoom dealt 13 damage to enemy1");
        AutoTestAssert.Equal(e2Before - 13, enemy2.CurrentHp, "Dead On ImpendingDoom dealt 13 damage to enemy2");
        // Horror prevented Bruise from wearing off on Turn 1 end, then Horror decremented to 0 on Enemy turn start!
        AutoTestAssert.Equal(8, enemy1.GetPowerAmount<BruisePower>(), "Horror kept Bruise(8) alive into Round 2");
        AutoTestAssert.Equal(0, enemy1.GetPowerAmount<HorrorPower>(), "HorrorPower decremented and expired");
        // Drained reduced Round 2 Energy by 1 and expired!
        AutoTestAssert.Equal(player.MaxEnergy - 1, pcs.Energy, "HermitDrainedPower reduced Round 2 Energy by 1");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<HermitDrainedPower>(), "HermitDrainedPower expired after firing");
        // TakeAim applied ConcentratedPower at Round 2 start!
        AutoTestAssert.True(player.Creature.HasPower<ConcentratedPower>(), "TakeAimPower applied ConcentratedPower on Round 2");

        // Verify per-turn resets on Round 2: Shortfuse back to cost 3, Ricochet deals 7 (+8 Bruise) = 15 with 0 repeats!
        AutoTestAssert.Equal(3, sf.EnergyCost.GetWithModifiers(CostModifiers.All), "Shortfuse cost reset to 3 on Round 2");
        await ResetHandAndEnergy();
        var r2Ricochet = combatState.CreateCard<Ricochet>(player);
        await CardPileCmd.Add(r2Ricochet, PileType.Hand);
        totalHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(r2Ricochet.TryManualPlay(enemy1), "Play Ricochet on Round 2 with 0 Dead On triggers");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHpBefore - 15, enemy1.CurrentHp + enemy2.CurrentHp, "Ricochet reset to 0 repeats on Round 2 (7+8 Bruise=15)");

        // Verify off-center ImpendingDoom does NOT trigger at Round 2 end!
        await ResetHandAndEnergy();
        var offCenterDoom = combatState.CreateCard<ImpendingDoom>(player);
        await CardPileCmd.Add(offCenterDoom, PileType.Hand); // index 0 of 3 -> off-center!
        await CardPileCmd.Add(combatState.CreateCard<HermitDefend>(player), PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<HermitDefend>(player), PileType.Hand);
        e1Before = enemy1.CurrentHp;
        e2Before = enemy2.CurrentHp;

        // End Round 2 -> Round 3: off-center ImpendingDoom deals 0, and without HorrorPower, Bruise now wears off!
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(e1Before, enemy1.CurrentHp, "Off-center ImpendingDoom did not damage enemy1");
        AutoTestAssert.Equal(e2Before, enemy2.CurrentHp, "Off-center ImpendingDoom did not damage enemy2");
        AutoTestAssert.Equal(0, enemy1.GetPowerAmount<BruisePower>(), "Bruise wore off after Horror expired");
    }

    public static async Task TestRareCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHermit>("DFHRMR1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy1, 800m);
        await CreatureCmd.GainMaxHp(enemy2, 800m);

        async Task ResetHandAndEnergy()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy < 10)
                await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
        }

        // 1. BlackWind: deals damage equal to missing HP (25), exhausts.
        await ResetHandAndEnergy();
        await CreatureCmd.Damage(ctx, player.Creature, 25m, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        int missingHp = player.Creature.MaxHp - player.Creature.CurrentHp;
        AutoTestAssert.Equal(25, missingHp, "Player missing 25 HP");
        var blackWind = combatState.CreateCard<BlackWind>(player);
        await CardPileCmd.Add(blackWind, PileType.Hand);
        int hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(blackWind.TryManualPlay(enemy1), "Play BlackWind");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 25, enemy1.CurrentHp, "BlackWind dealt 25 missing-HP damage");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(blackWind), "BlackWind exhausted");

        // 2. Reprieve: heals 10 HP, exhausts.
        await ResetHandAndEnergy();
        var reprieve = combatState.CreateCard<Reprieve>(player);
        await CardPileCmd.Add(reprieve, PileType.Hand);
        int playerHpBefore = player.Creature.CurrentHp;
        AutoTestAssert.True(reprieve.TryManualPlay(null), "Play Reprieve");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(playerHpBefore + 10, player.Creature.CurrentHp, "Reprieve healed 10 HP");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(reprieve), "Reprieve exhausted");

        // 3. FinalCanter: deals 10 damage per Curse in hand (2 Curses -> 20), exhausts.
        await ResetHandAndEnergy();
        var fc = combatState.CreateCard<FinalCanter>(player);
        await CardPileCmd.Add(fc, PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<StsCards.Clumsy>(player), PileType.Hand);
        await CardPileCmd.Add(combatState.CreateCard<StsCards.Injury>(player), PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(fc.TryManualPlay(enemy1), "Play FinalCanter with 2 Curses");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 20, enemy1.CurrentHp, "FinalCanter dealt 2*10 = 20 damage");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(fc), "FinalCanter exhausted");

        // 4. FromBeyond: deals 5 HP loss to a random enemy per card in Exhaust Pile!
        await ResetHandAndEnergy();
        int exhaustCount = PileType.Exhaust.GetPile(player).Cards.Count;
        AutoTestAssert.True(exhaustCount >= 3, $"Expected >=3 exhausted cards, got {exhaustCount}");
        await CreatureCmd.GainBlock(enemy1, 50m, ValueProp.Unpowered, null);
        await CreatureCmd.GainBlock(enemy2, 50m, ValueProp.Unpowered, null);
        int totalHpBefore = enemy1.CurrentHp + enemy2.CurrentHp;
        var fromBeyond = combatState.CreateCard<FromBeyond>(player);
        await CardPileCmd.Add(fromBeyond, PileType.Hand);
        AutoTestAssert.True(fromBeyond.TryManualPlay(null), "Play FromBeyond");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHpBefore - exhaustCount * 5, enemy1.CurrentHp + enemy2.CurrentHp,
            $"FromBeyond dealt {exhaustCount}*5 HP loss ignoring enemy Block");
        await CreatureCmd.LoseBlock(ctx, enemy1, enemy1.Block, null);
        await CreatureCmd.LoseBlock(ctx, enemy2, enemy2.Block, null);

        // 5. Gambit: puts 2 random Attacks from Discard Pile into Hand costing 1 less this turn, exhausts.
        await ResetHandAndEnergy();
        foreach (var c in PileType.Discard.GetPile(player).Cards.ToList())
            await CardPileCmd.Add(c, PileType.Draw);
        var discAtk1 = combatState.CreateCard<HermitStrike>(player);
        var discAtk2 = combatState.CreateCard<Headshot>(player);
        var discSkill = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(discAtk1, PileType.Discard);
        await CardPileCmd.Add(discAtk2, PileType.Discard);
        await CardPileCmd.Add(discSkill, PileType.Discard);
        var gambit = combatState.CreateCard<Gambit>(player);
        await CardPileCmd.Add(gambit, PileType.Hand);
        AutoTestAssert.True(gambit.TryManualPlay(null), "Play Gambit");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.Hand.Cards.Contains(discAtk1) && pcs.Hand.Cards.Contains(discAtk2), "Gambit pulled both Attacks from Discard");
        AutoTestAssert.True(!pcs.Hand.Cards.Contains(discSkill), "Gambit ignored Skill in Discard");
        AutoTestAssert.Equal(0, discAtk1.EnergyCost.GetWithModifiers(CostModifiers.All), "Gambit reduced Attack cost 1 -> 0");
        AutoTestAssert.Equal(0, discAtk2.EnergyCost.GetWithModifiers(CostModifiers.All), "Gambit reduced Attack cost 1 -> 0");

        // 6. HeroicBravado: gains 1 Rugged, increases its own cost 1 -> 3 this combat.
        await ResetHandAndEnergy();
        var bravado = combatState.CreateCard<HeroicBravado>(player);
        await CardPileCmd.Add(bravado, PileType.Hand);
        AutoTestAssert.True(bravado.TryManualPlay(null), "Play HeroicBravado");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<RuggedPower>(), "HeroicBravado granted 1 Rugged");
        AutoTestAssert.Equal(3, bravado.EnergyCost.GetWithModifiers(CostModifiers.All), "HeroicBravado cost increased 1 -> 3");
        await PowerCmd.Remove<RuggedPower>(player.Creature);

        // 7. HighNoon: playing a Strike or Defend draws 1 card; non-Strike/Defend does not!
        await ResetHandAndEnergy();
        var highNoon = combatState.CreateCard<HighNoon>(player);
        var hnStrike = combatState.CreateCard<HermitStrike>(player);
        var hnDefend = combatState.CreateCard<HermitDefend>(player);
        var hnOther = combatState.CreateCard<Glare>(player);
        await CardPileCmd.Add(highNoon, PileType.Hand);
        await CardPileCmd.Add(hnStrike, PileType.Hand);
        await CardPileCmd.Add(hnDefend, PileType.Hand);
        await CardPileCmd.Add(hnOther, PileType.Hand);
        AutoTestAssert.True(highNoon.TryManualPlay(null), "Play HighNoon");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        int handCount = pcs.Hand.Cards.Count;
        AutoTestAssert.True(hnStrike.TryManualPlay(enemy1), "Play Strike with HighNoon");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handCount, pcs.Hand.Cards.Count, "HighNoon drew 1 card on Strike play");
        AutoTestAssert.True(hnDefend.TryManualPlay(null), "Play Defend with HighNoon");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handCount, pcs.Hand.Cards.Count, "HighNoon drew 1 card on Defend play");
        AutoTestAssert.True(hnOther.TryManualPlay(enemy1), "Play non-Strike/Defend with HighNoon");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handCount - 1, pcs.Hand.Cards.Count, "HighNoon did NOT draw on non-Strike/Defend");
        await PowerCmd.Remove<HighNoonPower>(player.Creature);
        await PowerCmd.Remove<WeakPower>(enemy1);
        await PowerCmd.Remove<VulnerablePower>(enemy1);

        // 8. Magnum: discards 6 cards when >6 in hand (exercises FromHandForDiscard), deals 6*6 = 36 damage!
        await ResetHandAndEnergy();
        var magnum = combatState.CreateCard<Magnum>(player);
        await CardPileCmd.Add(magnum, PileType.Hand);
        for (int i = 0; i < 7; i++)
            await CardPileCmd.Add(combatState.CreateCard<HermitDefend>(player), PileType.Hand);
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(magnum.TryManualPlay(enemy1), "Play Magnum with 7 other cards");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "Magnum discarded 6 of 7 other cards, leaving 1");
        AutoTestAssert.Equal(hpBefore - 36, enemy1.CurrentHp, "Magnum dealt 6*6 = 36 damage");

        // 9. Purgatory: deals 24 AOE damage to both enemies.
        await ResetHandAndEnergy();
        var purgatory = combatState.CreateCard<Purgatory>(player);
        await CardPileCmd.Add(purgatory, PileType.Hand);
        int e1Before = enemy1.CurrentHp;
        int e2Before = enemy2.CurrentHp;
        AutoTestAssert.True(purgatory.TryManualPlay(null), "Play Purgatory");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Before - 24, enemy1.CurrentHp, "Purgatory dealt 24 to enemy1");
        AutoTestAssert.Equal(e2Before - 24, enemy2.CurrentHp, "Purgatory dealt 24 to enemy2");

        // 10. Roughhouse (Dead On): deals 24 damage and gains 20 Block!
        await ResetHandAndEnergy();
        var roughhouse = combatState.CreateCard<Roughhouse>(player);
        await CardPileCmd.Add(roughhouse, PileType.Hand);
        int blockBefore = player.Creature.Block;
        hpBefore = enemy1.CurrentHp;
        AutoTestAssert.True(roughhouse.TryManualPlay(enemy1), "Play Dead On Roughhouse");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 24, enemy1.CurrentHp, "Roughhouse dealt 24 damage");
        AutoTestAssert.Equal(blockBefore + 20, player.Creature.Block, "Dead On Roughhouse gained 20 Block");

        // 11. RoundhouseKick: deals 13 AOE damage, exhausts, stuns non-attacking CalcifiedCultist, does NOT stun attacking Nibbit!
        await ResetHandAndEnergy();
        var cultist = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.CalcifiedCultist>(combatState);
        await CreatureCmd.GainMaxHp(cultist, 200m);
        AutoTestAssert.True(cultist.Monster is { IntendsToAttack: false }, "CalcifiedCultist starts with non-attacking intent");
        bool enemy1Attacking = enemy1.Monster?.IntendsToAttack ?? false;
        var rhk = combatState.CreateCard<RoundhouseKick>(player);
        await CardPileCmd.Add(rhk, PileType.Hand);
        e1Before = enemy1.CurrentHp;
        e2Before = enemy2.CurrentHp;
        int cultistBefore = cultist.CurrentHp;
        AutoTestAssert.True(rhk.TryManualPlay(null), "Play RoundhouseKick");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Before - 13, enemy1.CurrentHp, "RoundhouseKick dealt 13 to enemy1");
        AutoTestAssert.Equal(e2Before - 13, enemy2.CurrentHp, "RoundhouseKick dealt 13 to enemy2");
        AutoTestAssert.Equal(cultistBefore - 13, cultist.CurrentHp, "RoundhouseKick dealt 13 to CalcifiedCultist");
        AutoTestAssert.Equal("STUNNED", cultist.Monster?.NextMove?.Id, "RoundhouseKick stunned non-attacking CalcifiedCultist");
        if (enemy1Attacking)
            AutoTestAssert.True(enemy1.Monster?.NextMove?.Id != "STUNNED", "RoundhouseKick did NOT stun attacking enemy1");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(rhk), "RoundhouseKick exhausted");
        // Kill cultist cleanly so it doesn't interfere with later assertions.
        await CreatureCmd.Kill(cultist);

        // 12. DeadOrAlive: X-cost at 2 energy -> kills low-HP enemy2 -> gains +15 Bounty Gold, exhausts!
        await ResetHandAndEnergy();
        await CreatureCmd.Damage(ctx, enemy2, enemy2.CurrentHp - 10, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        await PlayerCmd.SetEnergy(2, player);
        int goldBefore = player.Gold;
        var doa = combatState.CreateCard<DeadOrAlive>(player);
        await CardPileCmd.Add(doa, PileType.Hand);
        AutoTestAssert.True(doa.TryManualPlay(enemy2), "Play DeadOrAlive X=2 for Fatal kill");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(enemy2.IsDead, "DeadOrAlive killed enemy2");
        AutoTestAssert.Equal(goldBefore + 15, player.Gold, "Fatal DeadOrAlive granted 15 Bounty Gold");
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(doa), "DeadOrAlive exhausted");

        // 13. Dissolve + Adapt + EternalForm + FatalDesire + OverwhelmingPower across turn transition!
        await ResetHandAndEnergy();
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var dissolve = combatState.CreateCard<Dissolve>(player);
        var adapt = combatState.CreateCard<Adapt>(player);
        var eternal = combatState.CreateCard<EternalForm>(player);
        var fatalDesire = combatState.CreateCard<FatalDesire>(player);
        var overwhelming = combatState.CreateCard<OverwhelmingPower>(player);
        await CardPileCmd.Add(dissolve, PileType.Hand);
        await CardPileCmd.Add(adapt, PileType.Hand);
        await CardPileCmd.Add(eternal, PileType.Hand);
        await CardPileCmd.Add(fatalDesire, PileType.Hand);
        await CardPileCmd.Add(overwhelming, PileType.Hand);

        AutoTestAssert.True(dissolve.TryManualPlay(null), "Play Dissolve");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(18, player.Creature.Block, "Dissolve gained 18 Block");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<BlurPower>(), "Dissolve applied 2 BlurPower");

        AutoTestAssert.True(adapt.TryManualPlay(null), "Play Adapt");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(eternal.TryManualPlay(null), "Play EternalForm");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(fatalDesire.TryManualPlay(null), "Play FatalDesire");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        int energyBeforeOverwhelming = pcs.Energy;
        AutoTestAssert.True(overwhelming.TryManualPlay(null), "Play OverwhelmingPower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeOverwhelming - 1 + 3, pcs.Energy, "OverwhelmingPower gained 3 Energy");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<OverwhelmingPowerPower>(), "OverwhelmingPower applied 4 debuff");

        // Prepare known 1-cost cards on top of draw pile for EternalForm + FatalDesire + Adapt on Round 2!
        var drawTarget = combatState.CreateCard<HermitStrike>(player);
        for (int i = 0; i < 7; i++)
            await CardPileCmd.Add(combatState.CreateCard<HermitDefend>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(drawTarget, PileType.Draw, CardPilePosition.Top);

        // Give player huge Block so enemy1 attack doesn't touch HP, isolating OverwhelmingPower's 4 HP loss!
        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        await PlayerCmd.SetEnergy(0, player);
        playerHpBefore = player.Creature.CurrentHp;

        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, drawTarget)))
        {
            await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        }

        // OverwhelmingPower dealt 4 HP loss because Turn 1 ended with 0 Energy!
        AutoTestAssert.Equal(playerHpBefore - 4, player.Creature.CurrentHp, "OverwhelmingPower dealt 4 HP loss at 0 Energy");
        // Dissolve BlurPower preserved Block into Round 2 (and decremented 2 -> 1)!
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<BlurPower>(), "BlurPower decremented 2 -> 1");
        AutoTestAssert.True(player.Creature.Block >= 30, $"BlurPower preserved Block into Round 2 (got {player.Creature.Block})");
        // Adapt exhausted drawTarget!
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(drawTarget), "AdaptPower exhausted chosen card on Turn 2");
        // FatalDesire drew 2 extra cards (5 + 2 = 7 drawn, -1 exhausted by Adapt + 1 Injury = 7 in hand)!
        AutoTestAssert.Equal(7, pcs.Hand.Cards.Count, "FatalDesire drew 2 extra cards (+1 Injury -1 Adapt = 7 in hand)");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is StsCards.Injury), "FatalDesirePower added Injury to hand");
        // EternalPower discounted ONLY the first 4 playable cards drawn (1 Strike exhausted + 3 Defends = 3 zero-cost Defends, 3 one-cost Defends)!
        int zeroCostDefends = pcs.Hand.Cards.OfType<HermitDefend>().Count(d => d.EnergyCost.GetWithModifiers(CostModifiers.All) == 0);
        int oneCostDefends = pcs.Hand.Cards.OfType<HermitDefend>().Count(d => d.EnergyCost.GetWithModifiers(CostModifiers.All) == 1);
        AutoTestAssert.Equal(3, zeroCostDefends, "EternalPower discounted exactly 4 drawn cards (drawTarget + 3 Defends)");
        AutoTestAssert.Equal(3, oneCostDefends, "EternalPower stopped discounting after the 4th card (3 Defends still cost 1)");

        // End Round 2 WITH Energy > 0 -> OverwhelmingPower must NOT deal HP loss!
        await PlayerCmd.SetEnergy(3, player);
        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        playerHpBefore = player.Creature.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(playerHpBefore, player.Creature.CurrentHp, "OverwhelmingPower did NOT deal HP loss with >0 Energy");
    }

    public static async Task TestRelicsAndPotionsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHermit>("DFHRMRP");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy1, 500m);
        await CreatureCmd.GainMaxHp(enemy2, 500m);

        async Task ResetHandAndEnergy()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy < 10)
                await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
        }

        // 1. ClaspedLocket + StraightRazor (both Remove AND Transform!):
        //    ClaspedLocket replaces OldLocket and adds 2 Injuries to Deck; StraightRazor heals 15 HP on Remove and 15 HP on Transform!
        AutoTestAssert.True(ModelDb.Relic<ClaspedLocket>().IsAllowed(player.RunState), "ClaspedLocket allowed while owning OldLocket");
        int injuriesBefore = player.Deck.Cards.Count(c => c is StsCards.Injury);
        var clasped = await RelicCmd.Obtain<ClaspedLocket>(player);
        AutoTestAssert.True(!player.Relics.OfType<OldLocket>().Any(), "ClaspedLocket replaced OldLocket");
        AutoTestAssert.Equal(injuriesBefore + 2, player.Deck.Cards.Count(c => c is StsCards.Injury), "ClaspedLocket added 2 Injuries to deck");

        await RelicCmd.Obtain<StraightRazor>(player);
        await CreatureCmd.Damage(ctx, player.Creature, 40m, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        int hpBeforeRazor = player.Creature.CurrentHp;
        var deckInjury1 = player.Deck.Cards.OfType<StsCards.Injury>().First();
        await CardPileCmd.RemoveFromDeck(deckInjury1, showPreview: false);
        AutoTestAssert.Equal(hpBeforeRazor + 15, player.Creature.CurrentHp, "StraightRazor healed 15 HP on deck card removal");
        var deckInjury2 = player.Deck.Cards.OfType<StsCards.Injury>().First();
        await CardCmd.TransformTo<HermitStrike>(deckInjury2);
        AutoTestAssert.Equal(hpBeforeRazor + 30, player.Creature.CurrentHp, "StraightRazor healed 15 HP on deck card Transform");

        // 2. ClaspedLocket + CharredGlove in combat:
        //    First Curse drawn exhausts + draws 2 cards AND triggers CharredGlove (+3 Vigor); 2nd Curse drawn stays in hand + triggers CharredGlove (+3 Vigor)!
        await ResetHandAndEnergy();
        await RelicCmd.Obtain<CharredGlove>(player);
        var c1 = combatState.CreateCard<StsCards.Clumsy>(player);
        var c2 = combatState.CreateCard<StsCards.Injury>(player);
        var filler1 = combatState.CreateCard<HermitDefend>(player);
        var filler2 = combatState.CreateCard<HermitDefend>(player);
        await CardPileCmd.Add(c2, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(filler2, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(filler1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(c1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Draw(ctx, 1, player);
        AutoTestAssert.True(PileType.Exhaust.GetPile(player).Cards.Contains(c1), "ClaspedLocket exhausted first Curse drawn this turn");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "ClaspedLocket drew 2 replacement cards");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<VigorPower>(), "CharredGlove granted 3 Vigor on Curse draw");

        await CardPileCmd.Draw(ctx, 1, player);
        AutoTestAssert.True(pcs.Hand.Cards.Contains(c2), "ClaspedLocket did NOT exhaust second Curse same turn");
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<VigorPower>(), "CharredGlove granted +3 Vigor on 2nd Curse draw");
        await PowerCmd.Remove<VigorPower>(player.Creature);
        await RelicCmd.Remove(clasped);

        // 3. BlackPowder: Dead On deals 2 damage to ALL enemies, and 4 (2x2) with Snipe!
        await ResetHandAndEnergy();
        await RelicCmd.Obtain<BlackPowder>(player);
        var deadeye = combatState.CreateCard<Deadeye>(player);
        await CardPileCmd.Add(deadeye, PileType.Hand);
        int e1Before = enemy1.CurrentHp;
        int e2Before = enemy2.CurrentHp;
        AutoTestAssert.True(deadeye.TryManualPlay(enemy1), "Play Dead On Deadeye with BlackPowder");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Before - 5 - 2, enemy1.CurrentHp, "BlackPowder dealt +2 damage to enemy1");
        AutoTestAssert.Equal(e2Before - 2, enemy2.CurrentHp, "BlackPowder dealt 2 damage to enemy2");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        await ResetHandAndEnergy();
        await PowerCmd.Apply<SnipePower>(ctx, player.Creature, 1m, player.Creature, null);
        var deadeye2 = combatState.CreateCard<Deadeye>(player);
        await CardPileCmd.Add(deadeye2, PileType.Hand);
        e1Before = enemy1.CurrentHp;
        e2Before = enemy2.CurrentHp;
        AutoTestAssert.True(deadeye2.TryManualPlay(enemy1), "Play Sniped Dead On Deadeye with BlackPowder");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Before - 5 - 4, enemy1.CurrentHp, "Sniped BlackPowder dealt +4 damage to enemy1");
        AutoTestAssert.Equal(e2Before - 4, enemy2.CurrentHp, "Sniped BlackPowder dealt 4 damage to enemy2");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        // 4. Horseshoe: reduces incoming Weak/Frail/Vulnerable by 1 (1 -> 0, 2 -> 1); leaves other debuffs untouched!
        await RelicCmd.Obtain<Horseshoe>(player);
        await PowerCmd.Apply<WeakPower>(ctx, player.Creature, 1m, enemy1, null);
        await PowerCmd.Apply<VulnerablePower>(ctx, player.Creature, 2m, enemy1, null);
        await PowerCmd.Apply<FrailPower>(ctx, player.Creature, 2m, enemy1, null);
        await PowerCmd.Apply<BruisePower>(ctx, player.Creature, 3m, enemy1, null);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<WeakPower>(), "Horseshoe reduced 1 Weak -> 0");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<VulnerablePower>(), "Horseshoe reduced 2 Vulnerable -> 1");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<FrailPower>(), "Horseshoe reduced 2 Frail -> 1");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<BruisePower>(), "Horseshoe left non-Weak/Frail/Vuln debuff (Bruise) untouched");
        await PowerCmd.Remove<VulnerablePower>(player.Creature);
        await PowerCmd.Remove<FrailPower>(player.Creature);
        await PowerCmd.Remove<BruisePower>(player.Creature);

        // 5. RedScarf: gains 3 Block on NEW enemy debuff, 0 on stacking existing debuff, 0 when blocked by Artifact, +3 on negative Strength!
        await RelicCmd.Obtain<RedScarf>(player);
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await PowerCmd.Apply<ArtifactPower>(ctx, enemy1, 1m, enemy1, null);
        await PowerCmd.Apply<WeakPower>(ctx, enemy1, 1m, player.Creature, null);
        AutoTestAssert.Equal(0, player.Creature.Block, "RedScarf did NOT trigger when Artifact blocked Weak");
        await PowerCmd.Apply<WeakPower>(ctx, enemy1, 1m, player.Creature, null);
        AutoTestAssert.Equal(3, player.Creature.Block, "RedScarf gained 3 Block on new Weak debuff");
        await PowerCmd.Apply<WeakPower>(ctx, enemy1, 1m, player.Creature, null);
        AutoTestAssert.Equal(3, player.Creature.Block, "RedScarf did NOT trigger on stacking existing Weak debuff");
        await PowerCmd.Apply<StrengthPower>(ctx, enemy1, -1m, player.Creature, null);
        AutoTestAssert.Equal(6, player.Creature.Block, "RedScarf gained 3 Block on new negative Strength debuff");
        await PowerCmd.Remove<WeakPower>(enemy1);
        await PowerCmd.Remove<StrengthPower>(enemy1);

        // 6. PetGhost + RuggedPower ordering:
        //    (a) Non-lethal hit does NOT consume PetGhost;
        //    (b) At 5 HP with RuggedPower taking 15 powered attack damage, Rugged caps to 2 (non-lethal -> 3 HP) and PetGhost stays unused!
        //    (c) Lethal hit consumes PetGhost and preserves HP; 2nd lethal hit verified at end of test!
        var petGhost = await RelicCmd.Obtain<PetGhost>(player);
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        int hpStartGhost = player.Creature.CurrentHp;
        await CreatureCmd.Damage(ctx, player.Creature, 2m, ValueProp.Unblockable | ValueProp.Unpowered, enemy1, null, null);
        AutoTestAssert.Equal(hpStartGhost - 2, player.Creature.CurrentHp, "Non-lethal damage bypassed PetGhost");
        AutoTestAssert.Equal(RelicStatus.Normal, petGhost.Status, "Non-lethal damage did NOT consume PetGhost");

        // Drop to 5 HP, grant 1 Rugged, hit for 15 powered attack damage -> Rugged reduces to 2 (3 HP), PetGhost stays Normal!
        await CreatureCmd.Damage(ctx, player.Creature, player.Creature.CurrentHp - 5, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        await PowerCmd.Apply<RuggedPower>(ctx, player.Creature, 1m, player.Creature, null);
        await CreatureCmd.Damage(ctx, player.Creature, 15m, ValueProp.Move, enemy1, null, null);
        AutoTestAssert.Equal(3, player.Creature.CurrentHp, "Rugged capped 15 attack damage to 2 before PetGhost (5 -> 3 HP)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<RuggedPower>(), "RuggedPower consumed");
        AutoTestAssert.Equal(RelicStatus.Normal, petGhost.Status, "PetGhost NOT wasted when Rugged made hit non-lethal");

        // Now lethal hit (50 damage at 3 HP) -> PetGhost prevents lethal HP loss!
        await CreatureCmd.Damage(ctx, player.Creature, 50m, ValueProp.Unblockable | ValueProp.Unpowered, enemy1, null, null);
        AutoTestAssert.True(player.Creature.IsAlive, "PetGhost kept player alive on first lethal hit");
        AutoTestAssert.Equal(3, player.Creature.CurrentHp, "PetGhost prevented lethal HP loss completely");
        AutoTestAssert.Equal(RelicStatus.Disabled, petGhost.Status, "PetGhost disabled after first use");
        // Restore HP for remaining tests.
        await CreatureCmd.Heal(player.Creature, player.Creature.MaxHp - player.Creature.CurrentHp);

        // 7. BartenderGlass (Shotglass) + all 3 Hermit Potions (Tonic, BlackBile, Eclipse)!
        //    Mid-combat obtain auto-initializes counter to 2 without manual BeforeCombatStart(); 3rd potion grants 0 potions!
        var shotglass = await RelicCmd.Obtain<BartenderGlass>(player);
        AutoTestAssert.Equal(2, shotglass.DisplayAmount, "Shotglass mid-combat obtain initialized counter to 2");
        foreach (var existing in player.Potions.ToList())
            await PotionCmd.Discard(existing);

        var tonic = ModelDb.Potion<Tonic>().ToMutable();
        await PotionCmd.TryToProcure(tonic, player);
        await tonic.OnUseWrapper(ctx, player.Creature);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<RuggedPower>(), "Tonic granted 1 Rugged");
        AutoTestAssert.Equal(1, player.Potions.Count(), "Shotglass granted 1st random potion");
        AutoTestAssert.Equal(1, shotglass.DisplayAmount, "Shotglass counter decremented 2 -> 1");
        await PowerCmd.Remove<RuggedPower>(player.Creature);
        foreach (var existing in player.Potions.ToList())
            await PotionCmd.Discard(existing);

        var blackBile = ModelDb.Potion<BlackBile>().ToMutable();
        await PotionCmd.TryToProcure(blackBile, player);
        await blackBile.OnUseWrapper(ctx, enemy1);
        AutoTestAssert.Equal(6, enemy1.GetPowerAmount<BruisePower>(), "BlackBile applied 6 Bruise");
        AutoTestAssert.Equal(1, player.Potions.Count(), "Shotglass granted 2nd random potion");
        AutoTestAssert.Equal(RelicStatus.Disabled, shotglass.Status, "Shotglass disabled after 2 triggers");
        await PowerCmd.Remove<BruisePower>(enemy1);
        foreach (var existing in player.Potions.ToList())
            await PotionCmd.Discard(existing);

        await ResetHandAndEnergy();
        var exhaustedMalice = combatState.CreateCard<Malice>(player);
        await CardPileCmd.Add(exhaustedMalice, PileType.Exhaust);
        var eclipse = ModelDb.Potion<Eclipse>().ToMutable();
        await PotionCmd.TryToProcure(eclipse, player);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, exhaustedMalice)))
        {
            await eclipse.OnUseWrapper(ctx, player.Creature);
        }
        AutoTestAssert.True(pcs.Hand.Cards.Contains(exhaustedMalice), "Eclipse returned exhausted card to hand");
        AutoTestAssert.Equal(0, exhaustedMalice.EnergyCost.GetWithModifiers(CostModifiers.All), "Eclipse set returned card cost to 0");
        AutoTestAssert.Equal(0, player.Potions.Count(), "Disabled Shotglass did NOT grant a 3rd potion");

        // 8. BrassTacks + Spyglass + DentedPlate + RyeStalk across turn transition!
        await ResetHandAndEnergy();
        var brassTacks = await RelicCmd.Obtain<BrassTacks>(player);
        await RelicCmd.Obtain<Spyglass>(player);
        await RelicCmd.Obtain<DentedPlate>(player);
        await RelicCmd.Obtain<RyeStalk>(player);

        // Player-turn HP loss must NOT trigger RyeStalk!
        int handBeforeSelfDamage = pcs.Hand.Cards.Count;
        await CreatureCmd.Damage(ctx, player.Creature, 2m, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        AutoTestAssert.Equal(handBeforeSelfDamage, pcs.Hand.Cards.Count, "RyeStalk did NOT trigger on player-turn HP loss");

        // Drop player HP to <= 50% for DentedPlate (with plenty of HP left to survive enemy attacks!)
        int targetHp = player.Creature.MaxHp / 2;
        if (player.Creature.CurrentHp > targetHp)
        {
            await CreatureCmd.Damage(ctx, player.Creature, player.Creature.CurrentHp - targetHp, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        }
        await PowerCmd.Apply<ConcentratedPower>(ctx, player.Creature, 1m, player.Creature, null);
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        for (int i = 0; i < 10; i++)
            await CardPileCmd.Add(combatState.CreateCard<HermitDefend>(player), PileType.Draw, CardPilePosition.Top);

        int hpBeforeTurnEnd = player.Creature.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);

        // Spyglass (+1 EnergyNextTurnPower) + DentedPlate (+1 Energy) -> MaxEnergy + 2!
        AutoTestAssert.Equal(player.MaxEnergy + 2, pcs.Energy, "Spyglass + DentedPlate granted +2 Energy on Round 2");
        // Enemy attacks broke through BrassTacks's 2 Block -> RyeStalk drew on enemy turn + DentedPlate drew on Round 2 start!
        AutoTestAssert.True(player.Creature.CurrentHp < hpBeforeTurnEnd, "Enemy dealt unblocked damage during enemy turn");
        AutoTestAssert.True(pcs.Hand.Cards.Count >= 7, $"DentedPlate (+1) + RyeStalk (>=1) drew extra cards (hand={pcs.Hand.Cards.Count})");

        // Verify BrassTacks gained 2 Block directly via BeforeSideTurnEnd!
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await brassTacks.BeforeSideTurnEnd(ctx, CombatSide.Player, [player.Creature]);
        AutoTestAssert.Equal(2, player.Creature.Block, "BrassTacks gained 2 Block at end of turn");

        // 9. BloodyTooth: 0 on non-Elite room, +7 HP & +35 Gold on Elite room!
        var bloodyTooth = await RelicCmd.Obtain<BloodyTooth>(player);
        int hpBeforeTooth = player.Creature.CurrentHp;
        int goldBeforeTooth = player.Gold;
        var currentRoom = AutoTestAssert.NotNull(player.RunState.CurrentRoom as CombatRoom, "Current CombatRoom");
        await bloodyTooth.AfterCombatVictory(currentRoom);
        AutoTestAssert.Equal(goldBeforeTooth, player.Gold, "BloodyTooth did NOT trigger on non-Elite combat");
        AutoTestAssert.Equal(hpBeforeTooth, player.Creature.CurrentHp, "BloodyTooth did NOT heal on non-Elite combat");

        var eliteEncounter = ModelDb.AllEncounters.First(e => e.RoomType == RoomType.Elite).ToMutable();
        var eliteRoom = new CombatRoom(eliteEncounter, player.RunState);
        await bloodyTooth.AfterCombatVictory(eliteRoom);
        AutoTestAssert.Equal(goldBeforeTooth + 35, player.Gold, "BloodyTooth granted +35 Gold on Elite victory");
        AutoTestAssert.Equal(Math.Min(player.Creature.MaxHp, hpBeforeTooth + 7), player.Creature.CurrentHp, "BloodyTooth healed +7 HP on Elite victory");

        // 10. PetGhost 2nd lethal hit in the SAME combat is NOT prevented!
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await CreatureCmd.Damage(ctx, player.Creature, player.Creature.CurrentHp + 50, ValueProp.Unblockable | ValueProp.Unpowered, enemy1, null, null);
        AutoTestAssert.True(player.Creature.IsDead, "Disabled PetGhost did NOT prevent 2nd lethal hit in same combat");
    }
}
