using Downfall.Hermit;
using Downfall.Hermit.Cards;
using Downfall.Hermit.Powers;
using Downfall.Hermit.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;
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
}
