using Downfall.Champ;
using Downfall.Champ.Cards;
using Downfall.Champ.Powers;
using Downfall.Champ.Relics;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Testing;

internal static class ChampTests
{
    public static Task TestVisualsRuntime() =>
        DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheChamp>(
            "DFCHP00",
            ChampStances.IsValidChampSpritePath);

    public static async Task TestStarterCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheChamp>("DFCHP01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.GainMaxHp(enemy, 1000m);

        // 1. Starter stats, deck, relic, and Turn 1 Crown's Call in Hand.
        AutoTestAssert.Equal(80, player.Creature.MaxHp, "The Champ starts with 80 Max HP");
        AutoTestAssert.Equal(10, PileType.Deck.GetPile(player).Cards.Count, "Starter deck has 10 cards");
        AutoTestAssert.True(player.Relics.OfType<ChampionCrown>().Any(), "Starts with ChampionCrown");
        var crownCard = pcs.Hand.Cards.OfType<StanceDanceCrown>().FirstOrDefault();
        AutoTestAssert.NotNull(crownCard, "ChampionCrown added StanceDanceCrown to Turn 1 Hand");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "Starts in Neutral stance");

        async Task ResetHandAndEnergy(int energy = 10)
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy > energy)
                await PlayerCmd.LoseEnergy(pcs.Energy - energy, player);
            else if (pcs.Energy < energy)
                await PlayerCmd.GainEnergy(energy - pcs.Energy, player);
            if (player.Creature.Block > 0)
                await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        }

        // 2. Execute is unplayable in Neutral stance.
        await ResetHandAndEnergy();
        var exec = combatState.CreateCard<Execute>(player);
        await CardPileCmd.Add(exec, PileType.Hand);
        AutoTestAssert.True(!exec.TryManualPlay(enemy), "Execute cannot be played while in Neutral stance");

        // 3. BerserkersShout enters Berserker Stance with 3 charges (does not spend a charge on entry!) and grants 2 Vigor.
        var bShout = combatState.CreateCard<BerserkersShout>(player);
        await CardPileCmd.Add(bShout, PileType.Hand);
        AutoTestAssert.True(bShout.TryManualPlay(null), "Play BerserkersShout from Neutral");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(ChampStanceKind.Berserker, ChampStances.GetCurrentStance(player.Creature), "Entered Berserker Stance");
        var bStance = AutoTestAssert.NotNull(player.Creature.GetPower<BerserkerStancePower>(), "BerserkerStancePower present");
        AutoTestAssert.Equal(3, bStance.DisplayAmount, "Berserker Stance starts with 3 charges");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<VigorPower>(), "BerserkersShout granted 2 Vigor");

        var body = NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.Visuals?.Body as Sprite2D;
        AutoTestAssert.NotNull(body, "Champ Sprite2D body exists");
        AutoTestAssert.Equal(ChampStances.SpriteTexturePathFor(ChampStanceKind.Berserker), body!.Texture?.ResourcePath, "Combat sprite swapped to Berserker pose");

        // 4. Playing 4 Skills spends 3 charges (+2 Vigor each -> 2+6 = 8 Vigor), 4th grants 0; same-stance BerserkersShout does NOT reset charges!
        for (int i = 0; i < 4; i++)
        {
            var def = combatState.CreateCard<ChampDefend>(player);
            await CardPileCmd.Add(def, PileType.Hand);
            AutoTestAssert.True(def.TryManualPlay(null), $"Play Defend #{i + 1} in Berserker");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(0, bStance.DisplayAmount, "Berserker Stance has 0 charges after 4 Skills");
        AutoTestAssert.Equal(8, player.Creature.GetPowerAmount<VigorPower>(), "3 Skill Bonus charges granted +6 Vigor (2 -> 8)");

        var bShoutSame = combatState.CreateCard<BerserkersShout>(player);
        await CardPileCmd.Add(bShoutSame, PileType.Hand);
        AutoTestAssert.True(bShoutSame.TryManualPlay(null), "Play BerserkersShout while already in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, bStance.DisplayAmount, "Re-entering same Stance did NOT reset charges");
        AutoTestAssert.Equal(10, player.Creature.GetPowerAmount<VigorPower>(), "BerserkersShout still granted its own +2 Vigor (8 -> 10)");

        // 5. Execute in Berserker: deals (6+10)x2 = 32 damage (StS 2 Vigor applies to all hits of AttackCommand), grants +1 Strength, exits Stance!
        int enemyHp = enemy.CurrentHp;
        AutoTestAssert.True(exec.TryManualPlay(enemy), "Play Execute in Berserker Stance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(enemyHp - 32, enemy.CurrentHp, "Execute dealt (6 + 10 Vigor) x 2 = 32 damage");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "Berserker Finisher Bonus granted +1 Strength");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "Execute exited Berserker Stance");
        AutoTestAssert.Equal(ChampStances.SpriteTexturePathFor(ChampStanceKind.None), body.Texture?.ResourcePath, "Combat sprite returned to Idle pose");

        // 6. DefensiveShout enters Defensive Stance with 3 charges + 3 Counter; Defend spends 1 charge -> +3 Counter (6 total).
        await ResetHandAndEnergy();
        var dShout = combatState.CreateCard<DefensiveShout>(player);
        await CardPileCmd.Add(dShout, PileType.Hand);
        AutoTestAssert.True(dShout.TryManualPlay(null), "Play DefensiveShout");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(ChampStanceKind.Defensive, ChampStances.GetCurrentStance(player.Creature), "Entered Defensive Stance");
        AutoTestAssert.Equal(ChampStances.SpriteTexturePathFor(ChampStanceKind.Defensive), body.Texture?.ResourcePath, "Combat sprite swapped to Defensive pose");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<CounterPower>(), "DefensiveShout granted 3 Counter");

        var defInDef = combatState.CreateCard<ChampDefend>(player);
        await CardPileCmd.Add(defInDef, PileType.Hand);
        AutoTestAssert.True(defInDef.TryManualPlay(null), "Play Defend in Defensive Stance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPower<DefensiveStancePower>()!.DisplayAmount, "Defensive Stance spent 1 charge (3 -> 2)");
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<CounterPower>(), "Defensive Skill Bonus granted +3 Counter (3 -> 6)");

        // 7. Multi-hit enemy powered attack triggers CounterPower ONCE (even when fully blocked!) -> removes Counter, adds 1 6-dmg Riposte!
        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Unpowered, null);
        await CreatureCmd.Damage(ctx, player.Creature, 5m, ValueProp.Move, enemy, null, null);
        await CreatureCmd.Damage(ctx, player.Creature, 5m, ValueProp.Move, enemy, null, null);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<CounterPower>(), "CounterPower removed after first hit");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.OfType<Riposte>().Count(), "Multi-hit attack only added 1 Riposte");
        var riposte = pcs.Hand.Cards.OfType<Riposte>().FirstOrDefault();
        AutoTestAssert.NotNull(riposte, "CounterPower added Riposte to Hand");
        AutoTestAssert.True(riposte!.Tags.Contains(CardTag.Strike), "Riposte carries CardTag.Strike");
        AutoTestAssert.Equal(6m, riposte.DynamicVars.Damage.BaseValue, "Riposte base damage equals snapshotted Counter (6)");
        enemyHp = enemy.CurrentHp;
        AutoTestAssert.True(riposte.TryManualPlay(enemy), "Play Riposte");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(enemyHp - 7, enemy.CurrentHp, "Riposte dealt 6 + 1 Strength = 7 damage");
        AutoTestAssert.Equal(PileType.Exhaust, riposte.Pile?.Type, "Riposte exhausted");

        // 8. Execute in Defensive Stance grants +6 Block and exits Stance!
        await ResetHandAndEnergy();
        var execDef = combatState.CreateCard<Execute>(player);
        await CardPileCmd.Add(execDef, PileType.Hand);
        AutoTestAssert.True(execDef.TryManualPlay(enemy), "Play Execute in Defensive Stance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, player.Creature.Block, "Defensive Finisher Bonus granted 6 Block");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "Execute exited Defensive Stance");

        // 9. StanceDanceCrown (base Exhausts; Upgraded goes to Discard!) + ChooseStanceAsync!
        using (DownfallAutoTestRunner.PreferCards(c => c is ChooseDefensiveStance))
        {
            var picked = await ChampStances.ChooseStanceAsync(player, combatState, ctx);
            AutoTestAssert.Equal(ChampStanceKind.Defensive, picked, "ChooseStanceAsync picked Defensive");
        }
        AutoTestAssert.Equal(ChampStanceKind.Defensive, ChampStances.GetCurrentStance(player.Creature), "Entered Defensive via ChooseStanceAsync");

        var crownPlay = combatState.CreateCard<StanceDanceCrown>(player);
        await CardPileCmd.Add(crownPlay, PileType.Hand);
        AutoTestAssert.True(crownPlay.TryManualPlay(null), "Play StanceDanceCrown from Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(ChampStanceKind.Berserker, ChampStances.GetCurrentStance(player.Creature), "StanceDanceCrown switched Defensive -> Berserker");
        AutoTestAssert.Equal(PileType.Exhaust, crownPlay.Pile?.Type, "Base StanceDanceCrown exhausted");

        var crownPlus = combatState.CreateCard<StanceDanceCrown>(player);
        CardCmd.Upgrade(crownPlus);
        await CardPileCmd.Add(crownPlus, PileType.Hand);
        AutoTestAssert.True(crownPlus.TryManualPlay(null), "Play StanceDanceCrown+ from Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(ChampStanceKind.Defensive, ChampStances.GetCurrentStance(player.Creature), "StanceDanceCrown+ switched Berserker -> Defensive");
        AutoTestAssert.Equal(PileType.Discard, crownPlus.Pile?.Type, "Upgraded StanceDanceCrown+ went to Discard instead of Exhaust");

        // 10. UltimateFormNextTurnPower(1) -> Turn 2 enters Ultimate Stance + locks via UltimateFormPower(1) -> Turn 3 exits to Neutral!
        await PowerCmd.Apply<UltimateFormNextTurnPower>(ctx, player.Creature, 1m, player.Creature, null);
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);

        AutoTestAssert.Equal(ChampStanceKind.Ultimate, ChampStances.GetCurrentStance(player.Creature), "Turn 2 entered Ultimate Stance");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<UltimateFormPower>(), "UltimateFormPower(1) active on Turn 2");
        AutoTestAssert.Equal(ChampStances.SpriteTexturePathFor(ChampStanceKind.Ultimate), body.Texture?.ResourcePath, "Combat sprite swapped to Ultimate pose");

        // Skill in Ultimate grants BOTH +2 Vigor AND +3 Counter!
        await ResetHandAndEnergy();
        if (player.Creature.GetPower<VigorPower>() is { } vClean) await PowerCmd.Remove(vClean);
        if (player.Creature.GetPower<CounterPower>() is { } cClean) await PowerCmd.Remove(cClean);
        var ultDef = combatState.CreateCard<ChampDefend>(player);
        await CardPileCmd.Add(ultDef, PileType.Hand);
        AutoTestAssert.True(ultDef.TryManualPlay(null), "Play Defend in Ultimate Stance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<VigorPower>(), "Ultimate Skill Bonus granted +2 Vigor");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<CounterPower>(), "Ultimate Skill Bonus granted +3 Counter");

        // Finisher in Ultimate grants BOTH +1 Strength AND +6 Block AND stays locked in Ultimate!
        int strBefore = player.Creature.GetPowerAmount<StrengthPower>();
        int blkBefore = player.Creature.Block;
        var execUlt = combatState.CreateCard<Execute>(player);
        await CardPileCmd.Add(execUlt, PileType.Hand);
        AutoTestAssert.True(execUlt.TryManualPlay(enemy), "Play Execute in Ultimate Stance");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(strBefore + 1, player.Creature.GetPowerAmount<StrengthPower>(), "Ultimate Finisher Bonus granted +1 Strength");
        AutoTestAssert.Equal(blkBefore + 6, player.Creature.Block, "Ultimate Finisher Bonus granted +6 Block");
        AutoTestAssert.Equal(ChampStanceKind.Ultimate, ChampStances.GetCurrentStance(player.Creature), "UltimateFormPower locked player in Ultimate Stance after Finisher");

        // End Turn 2 -> Turn 3: UltimateFormPower expires and exits Ultimate Stance cleanly!
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<UltimateFormPower>(), "UltimateFormPower expired after Turn 2");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "Exited Ultimate Stance back to Neutral on Turn 3");
        AutoTestAssert.Equal(ChampStances.SpriteTexturePathFor(ChampStanceKind.None), body.Texture?.ResourcePath, "Combat sprite returned to Idle pose");
    }

    public static async Task TestCommonCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheChamp>("DFCHP02");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        foreach (var e in combatState.HittableEnemies.ToList())
            await CreatureCmd.GainMaxHp(e, 2000m);

        async Task ResetCombatState(int energy = 10)
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            if (pcs.Energy > energy)
                await PlayerCmd.LoseEnergy(pcs.Energy - energy, player);
            else if (pcs.Energy < energy)
                await PlayerCmd.GainEnergy(energy - pcs.Energy, player);
            if (player.Creature.Block > 0)
                await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
            foreach (var p in player.Creature.Powers.ToList())
                await PowerCmd.Remove(p);
            foreach (var e in combatState.HittableEnemies.ToList())
            {
                foreach (var ep in e.Powers.ToList())
                    await PowerCmd.Remove(ep);
            }
        }

        // 1. AdrenalArmor: 7 Block + FlexPotionPower(2) -> +2 Strength!
        await ResetCombatState();
        var adrenal = combatState.CreateCard<AdrenalArmor>(player);
        await CardPileCmd.Add(adrenal, PileType.Hand);
        AutoTestAssert.True(adrenal.TryManualPlay(null), "Play AdrenalArmor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.Block, "AdrenalArmor gained 7 Block");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<FlexPotionPower>(), "AdrenalArmor applied FlexPotionPower(2)");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "AdrenalArmor temporary Strength = 2");

        // 2. BringItOn (Finisher): unplayable in Neutral; playable in Berserker -> +6 Block + 6 Counter + 1 Strength + exits Stance.
        await ResetCombatState();
        var bringIt = combatState.CreateCard<BringItOn>(player);
        await CardPileCmd.Add(bringIt, PileType.Hand);
        AutoTestAssert.True(!bringIt.TryManualPlay(null), "BringItOn unplayable in Neutral");
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        AutoTestAssert.True(bringIt.TryManualPlay(null), "Play BringItOn in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, player.Creature.Block, "BringItOn gained 6 Block");
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<CounterPower>(), "BringItOn gained 6 Counter");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "BringItOn triggered Berserker Finisher (+1 Strength)");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "BringItOn exited Stance");

        // 3. Circumvent: outside Defensive draws 2 & discards 2; inside Defensive draws 2 & discards 0!
        await ResetCombatState();
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<ChampStrike>(player), PileType.Draw);
        var circNeutral = combatState.CreateCard<Circumvent>(player);
        await CardPileCmd.Add(circNeutral, PileType.Hand);
        AutoTestAssert.True(circNeutral.TryManualPlay(null), "Play Circumvent outside Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, player.Creature.Block, "Circumvent gained 6 Block");
        AutoTestAssert.Equal(0, pcs.Hand.Cards.Count, "Circumvent drew 2 then discarded 2 outside Defensive");

        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<ChampStrike>(player), PileType.Draw);
        var circDef = combatState.CreateCard<Circumvent>(player);
        await CardPileCmd.Add(circDef, PileType.Hand);
        AutoTestAssert.True(circDef.TryManualPlay(null), "Play Circumvent inside Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Circumvent Defensive Combo skipped discard (kept 2 drawn cards)");

        // 4. CrownThrow (Crownarang): outside Berserker 8 dmg + 0 draw; inside Berserker 8 dmg + 2 draw!
        await ResetCombatState();
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<ChampStrike>(player), PileType.Draw);
        int hp = enemy.CurrentHp;
        var crownNeutral = combatState.CreateCard<CrownThrow>(player);
        await CardPileCmd.Add(crownNeutral, PileType.Hand);
        AutoTestAssert.True(crownNeutral.TryManualPlay(enemy), "Play CrownThrow outside Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 8, enemy.CurrentHp, "CrownThrow dealt 8 damage");
        AutoTestAssert.Equal(0, pcs.Hand.Cards.Count, "CrownThrow drew 0 outside Berserker");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        hp = enemy.CurrentHp;
        var crownBerserk = combatState.CreateCard<CrownThrow>(player);
        await CardPileCmd.Add(crownBerserk, PileType.Hand);
        AutoTestAssert.True(crownBerserk.TryManualPlay(enemy), "Play CrownThrow inside Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 8, enemy.CurrentHp, "CrownThrow dealt 8 damage in Berserker");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "CrownThrow Berserker Combo drew 2 cards");

        // 5. Encircle: 4 AOE dmg, draws 1 per enemy hit.
        await ResetCombatState();
        for (int i = 0; i < 5; i++)
            await CardPileCmd.Add(combatState.CreateCard<ChampStrike>(player), PileType.Draw);
        int enemyCount = combatState.HittableEnemies.Count;
        hp = enemy.CurrentHp;
        var encircle = combatState.CreateCard<Encircle>(player);
        await CardPileCmd.Add(encircle, PileType.Hand);
        AutoTestAssert.True(encircle.TryManualPlay(null), "Play Encircle");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 4, enemy.CurrentHp, "Encircle dealt 4 AOE damage");
        AutoTestAssert.Equal(enemyCount, pcs.Hand.Cards.Count, $"Encircle drew {enemyCount} card(s) for {enemyCount} enemy hit(s)");

        // 6. Endure: enters Defensive Stance; base uses Strength instead of Dexterity (6+3=9); upgraded uses both (8+3+2=13)!
        await ResetCombatState();
        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, 3m, player.Creature, null);
        await PowerCmd.Apply<DexterityPower>(ctx, player.Creature, 2m, player.Creature, null);
        var endureBase = combatState.CreateCard<Endure>(player);
        await CardPileCmd.Add(endureBase, PileType.Hand);
        AutoTestAssert.True(endureBase.TryManualPlay(null), "Play base Endure");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(ChampStanceKind.Defensive, ChampStances.GetCurrentStance(player.Creature), "Endure entered Defensive Stance");
        AutoTestAssert.Equal(9, player.Creature.Block, "Base Endure gained 6 + 3 Str (ignoring 2 Dex) = 9 Block");

        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var endurePlus = combatState.CreateCard<Endure>(player);
        CardCmd.Upgrade(endurePlus);
        await CardPileCmd.Add(endurePlus, PileType.Hand);
        AutoTestAssert.True(endurePlus.TryManualPlay(null), "Play upgraded Endure+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(13, player.Creature.Block, "Upgraded Endure+ gained 8 + 3 Str + 2 Dex = 13 Block");

        // 7. FlashCut: outside Defensive 6 dmg only; inside Defensive 6 dmg + 4 Counter + 5 Block.
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var flashNeutral = combatState.CreateCard<FlashCut>(player);
        await CardPileCmd.Add(flashNeutral, PileType.Hand);
        AutoTestAssert.True(flashNeutral.TryManualPlay(enemy), "Play FlashCut outside Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 6, enemy.CurrentHp, "FlashCut dealt 6 damage");
        AutoTestAssert.Equal(0, player.Creature.Block, "FlashCut granted 0 Block outside Defensive");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<CounterPower>(), "FlashCut granted 0 Counter outside Defensive");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        hp = enemy.CurrentHp;
        var flashDef = combatState.CreateCard<FlashCut>(player);
        await CardPileCmd.Add(flashDef, PileType.Hand);
        AutoTestAssert.True(flashDef.TryManualPlay(enemy), "Play FlashCut inside Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 6, enemy.CurrentHp, "FlashCut dealt 6 damage in Defensive");
        AutoTestAssert.Equal(5, player.Creature.Block, "FlashCut Defensive Combo gained 5 Block");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<CounterPower>(), "FlashCut Defensive Combo gained 4 Counter");

        // 8. FocusedDefense (Bob and Weave): 4 Block + 4 Vigor + enters Berserker Stance.
        await ResetCombatState();
        var bob = combatState.CreateCard<FocusedDefense>(player);
        await CardPileCmd.Add(bob, PileType.Hand);
        AutoTestAssert.True(bob.TryManualPlay(null), "Play FocusedDefense");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.Block, "FocusedDefense gained 4 Block");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<VigorPower>(), "FocusedDefense gained 4 Vigor");
        AutoTestAssert.Equal(ChampStanceKind.Berserker, ChampStances.GetCurrentStance(player.Creature), "FocusedDefense entered Berserker Stance");

        // 9. Haymaker (Piledriver, Finisher): 10 dmg + 2 Vulnerable + 2 Weak + Defensive Finisher (+6 Block) + exits Stance.
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        hp = enemy.CurrentHp;
        var haymaker = combatState.CreateCard<Haymaker>(player);
        await CardPileCmd.Add(haymaker, PileType.Hand);
        AutoTestAssert.True(haymaker.TryManualPlay(enemy), "Play Haymaker in Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 10, enemy.CurrentHp, "Haymaker dealt 10 damage");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "Haymaker applied 2 Vulnerable");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "Haymaker applied 2 Weak");
        AutoTestAssert.Equal(6, player.Creature.Block, "Haymaker triggered Defensive Finisher (+6 Block)");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "Haymaker exited Stance");

        // 10. ChampHeadbutt: 9 dmg + moves selected card from Discard Pile to top of Draw Pile!
        await ResetCombatState();
        var topTarget = combatState.CreateCard<Endure>(player);
        await CardPileCmd.Add(topTarget, PileType.Discard);
        hp = enemy.CurrentHp;
        var headbutt = combatState.CreateCard<ChampHeadbutt>(player);
        await CardPileCmd.Add(headbutt, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, topTarget)))
        {
            AutoTestAssert.True(headbutt.TryManualPlay(enemy), "Play ChampHeadbutt");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(hp - 9, enemy.CurrentHp, "ChampHeadbutt dealt 9 damage");
        AutoTestAssert.True(ReferenceEquals(PileType.Draw.GetPile(player).Cards.FirstOrDefault(), topTarget), "ChampHeadbutt placed selected card on top of Draw Pile");

        // 11. ChampPerfectedStrike: 8 + 2 * non-Exhausted strikeCount damage (Exhausted Riposte excluded!).
        await ResetCombatState();
        var exhaustedRiposte = combatState.CreateCard<Riposte>(player);
        await CardPileCmd.Add(exhaustedRiposte, PileType.Exhaust);
        var perfStrike = combatState.CreateCard<ChampPerfectedStrike>(player);
        await CardPileCmd.Add(perfStrike, PileType.Hand);
        int strikeCount = pcs.AllCards.Count(c => c.Tags.Contains(CardTag.Strike) && c.Pile?.Type != PileType.Exhaust);
        int expectedPerfDmg = 8 + 2 * strikeCount;
        hp = enemy.CurrentHp;
        AutoTestAssert.True(perfStrike.TryManualPlay(enemy), "Play ChampPerfectedStrike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - expectedPerfDmg, enemy.CurrentHp, $"ChampPerfectedStrike dealt 8 + 2*{strikeCount} = {expectedPerfDmg} damage (excluding Exhausted Riposte)");

        // 12. PreciseThrust: Neutral (6), Defensive (6 + 6 Block), Berserker (12), Ultimate (12 + 6 Block).
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var ptNeutral = combatState.CreateCard<PreciseThrust>(player);
        await CardPileCmd.Add(ptNeutral, PileType.Hand);
        AutoTestAssert.True(ptNeutral.TryManualPlay(enemy), "Play PreciseThrust in Neutral");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 6, enemy.CurrentHp, "PreciseThrust Neutral dealt 6");
        AutoTestAssert.Equal(0, player.Creature.Block, "PreciseThrust Neutral gained 0 Block");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Ultimate, ctx);
        hp = enemy.CurrentHp;
        var ptUlt = combatState.CreateCard<PreciseThrust>(player);
        await CardPileCmd.Add(ptUlt, PileType.Hand);
        AutoTestAssert.True(ptUlt.TryManualPlay(enemy), "Play PreciseThrust in Ultimate");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 12, enemy.CurrentHp, "PreciseThrust Ultimate dealt 6+6=12 damage");
        AutoTestAssert.Equal(6, player.Creature.Block, "PreciseThrust Ultimate gained 6 Block");

        // 13. RapidStrikes: 4x2 = 8 dmg + makes a 1-cost Strike in Hand cost 0 this turn!
        await ResetCombatState();
        var handStrike = combatState.CreateCard<ChampStrike>(player);
        await CardPileCmd.Add(handStrike, PileType.Hand);
        var rapid = combatState.CreateCard<RapidStrikes>(player);
        await CardPileCmd.Add(rapid, PileType.Hand);
        hp = enemy.CurrentHp;
        AutoTestAssert.True(rapid.TryManualPlay(enemy), "Play RapidStrikes");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 8, enemy.CurrentHp, "RapidStrikes dealt 4x2 = 8 damage");
        AutoTestAssert.Equal(0, handStrike.EnergyCost.GetWithModifiers(CostModifiers.Local), "RapidStrikes reduced Strike in Hand to 0 cost this turn");

        // 14. RisingSlash (Rising Strike): Retain; 5 dmg after non-Finisher, 5x2 = 10 dmg after Finisher!
        await ResetCombatState();
        var rising1 = combatState.CreateCard<RisingSlash>(player);
        AutoTestAssert.True(rising1.Keywords.Contains(CardKeyword.Retain), "RisingSlash has Retain");
        await CardPileCmd.Add(rising1, PileType.Hand);
        hp = enemy.CurrentHp;
        AutoTestAssert.True(rising1.TryManualPlay(enemy), "Play RisingSlash after non-Finisher");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 5, enemy.CurrentHp, "RisingSlash dealt 5 damage after non-Finisher");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        var finSetup = combatState.CreateCard<BringItOn>(player);
        await CardPileCmd.Add(finSetup, PileType.Hand);
        AutoTestAssert.True(finSetup.TryManualPlay(null), "Play BringItOn (Finisher) before RisingSlash");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        var rising2 = combatState.CreateCard<RisingSlash>(player);
        await CardPileCmd.Add(rising2, PileType.Hand);
        hp = enemy.CurrentHp;
        AutoTestAssert.True(rising2.TryManualPlay(enemy), "Play RisingSlash immediately after Finisher");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 10, enemy.CurrentHp, "RisingSlash dealt 5x2 = 10 damage after Finisher");

        // 15. ShieldSigil (Chain Lash) + DoubleStyleThisTurnPower + StanceDance (base & upgraded)!
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var sigil = combatState.CreateCard<ShieldSigil>(player);
        await CardPileCmd.Add(sigil, PileType.Hand);
        AutoTestAssert.True(sigil.TryManualPlay(enemy), "Play ShieldSigil");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 3, enemy.CurrentHp, "ShieldSigil dealt 3 damage");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DoubleStyleThisTurnPower>(), "ShieldSigil applied DoubleStyleThisTurnPower(2)");

        var danceBase = combatState.CreateCard<StanceDance>(player);
        await CardPileCmd.Add(danceBase, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is ChooseBerserkerStance))
        {
            AutoTestAssert.True(danceBase.TryManualPlay(null), "Play StanceDance choosing Berserker");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(ChampStanceKind.Berserker, ChampStances.GetCurrentStance(player.Creature), "StanceDance entered Berserker");
        AutoTestAssert.Equal(3, player.Creature.GetPower<BerserkerStancePower>()!.DisplayAmount, "StanceDance did not spend charges of newly entered Berserker Stance");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<VigorPower>(), "StanceDance triggered Berserker Skill Bonus boosted by Chain Lash (2 + 2 = 4 Vigor)");

        var dancePlus = combatState.CreateCard<StanceDance>(player);
        CardCmd.Upgrade(dancePlus);
        await CardPileCmd.Add(dancePlus, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is ChooseDefensiveStance))
        {
            AutoTestAssert.True(dancePlus.TryManualPlay(null), "Play StanceDance+ choosing Defensive");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(ChampStanceKind.Defensive, ChampStances.GetCurrentStance(player.Creature), "StanceDance+ entered Defensive");
        // Note: StanceDance+ itself is a Skill played while in Berserker (spends 1 Berserker charge), then switches to Defensive and triggers Defensive Skill Bonus twice: (3+2)*2 = 10 Counter!
        AutoTestAssert.Equal(10, player.Creature.GetPowerAmount<CounterPower>(), "StanceDance+ triggered Defensive Skill Bonus twice boosted by Chain Lash ((3+2)*2 = 10 Counter)");

        // 16. Taunt (base = single enemy, upgraded = ALL enemies).
        await ResetCombatState();
        var tauntBase = combatState.CreateCard<Taunt>(player);
        AutoTestAssert.Equal(TargetType.AnyEnemy, tauntBase.TargetType, "Base Taunt targets AnyEnemy");
        await CardPileCmd.Add(tauntBase, PileType.Hand);
        AutoTestAssert.True(tauntBase.TryManualPlay(enemy), "Play base Taunt");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "Base Taunt applied 1 Weak");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "Base Taunt applied 1 Vulnerable");

        var tauntPlus = combatState.CreateCard<Taunt>(player);
        CardCmd.Upgrade(tauntPlus);
        AutoTestAssert.Equal(TargetType.AllEnemies, tauntPlus.TargetType, "Upgraded Taunt+ targets AllEnemies");
        await CardPileCmd.Add(tauntPlus, PileType.Hand);
        AutoTestAssert.True(tauntPlus.TryManualPlay(null), "Play upgraded Taunt+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        foreach (var e in combatState.HittableEnemies)
        {
            AutoTestAssert.True(e.GetPowerAmount<WeakPower>() >= 1, "Taunt+ applied Weak to all enemies");
            AutoTestAssert.True(e.GetPowerAmount<VulnerablePower>() >= 1, "Taunt+ applied Vulnerable to all enemies");
        }

        // 17. TornadoPunch: outside Defensive 12 AOE only; inside Defensive 12 AOE + 7*hits Block!
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var tornadoNeutral = combatState.CreateCard<TornadoPunch>(player);
        await CardPileCmd.Add(tornadoNeutral, PileType.Hand);
        AutoTestAssert.True(tornadoNeutral.TryManualPlay(null), "Play TornadoPunch outside Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 12, enemy.CurrentHp, "TornadoPunch dealt 12 AOE damage");
        AutoTestAssert.Equal(0, player.Creature.Block, "TornadoPunch granted 0 Block outside Defensive");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        hp = enemy.CurrentHp;
        var tornadoDef = combatState.CreateCard<TornadoPunch>(player);
        await CardPileCmd.Add(tornadoDef, PileType.Hand);
        AutoTestAssert.True(tornadoDef.TryManualPlay(null), "Play TornadoPunch inside Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 12, enemy.CurrentHp, "TornadoPunch dealt 12 AOE damage in Defensive");
        AutoTestAssert.Equal(7 * enemyCount, player.Creature.Block, $"TornadoPunch Defensive Combo gained 7 * {enemyCount} Block");

        // 18. EnGarde + EnGardePower (broken -> BlockNextTurnPower on Turn 2; unbroken -> expires cleanly on Turn 3) + DoubleStyleThisTurnPower end-of-turn expiry!
        await ResetCombatState();
        await PowerCmd.Apply<DoubleStyleThisTurnPower>(ctx, player.Creature, 2m, player.Creature, null);
        var enGarde = combatState.CreateCard<EnGarde>(player);
        await CardPileCmd.Add(enGarde, PileType.Hand);
        AutoTestAssert.True(enGarde.TryManualPlay(null), "Play EnGarde");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.Block, "EnGarde gained 7 Block");
        AutoTestAssert.Equal(7, player.Creature.GetPowerAmount<EnGardePower>(), "EnGarde applied EnGardePower(7)");

        // Break player's Block -> EnGardePower converts into BlockNextTurnPower(7)!
        await CreatureCmd.Damage(ctx, player.Creature, 10m, ValueProp.Move, enemy, null, null);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<EnGardePower>(), "EnGardePower consumed on Block broken");
        AutoTestAssert.Equal(7, player.Creature.GetPowerAmount<BlockNextTurnPower>(), "EnGardePower applied BlockNextTurnPower(7)");

        // End Turn 1 -> Turn 2: DoubleStyleThisTurnPower expired; BlockNextTurnPower granted 7 Block!
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DoubleStyleThisTurnPower>(), "DoubleStyleThisTurnPower expired at end of Turn 1");
        AutoTestAssert.Equal(7, player.Creature.Block, "BlockNextTurnPower granted 7 Block on Turn 2");

        // Unbroken EnGarde on Turn 2 -> expires at end of round with 0 Block on Turn 3!
        var enGardeUnbroken = combatState.CreateCard<EnGarde>(player);
        await CardPileCmd.Add(enGardeUnbroken, PileType.Hand);
        AutoTestAssert.True(enGardeUnbroken.TryManualPlay(null), "Play unbroken EnGarde on Turn 2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        await CreatureCmd.GainBlock(player.Creature, 100m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<EnGardePower>(), "Unbroken EnGardePower expired at end of round");
        AutoTestAssert.Equal(0, player.Creature.Block, "Unbroken EnGarde did not grant next-turn Block");
    }
}
