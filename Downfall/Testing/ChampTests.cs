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

    public static async Task TestUncommonBatch1InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheChamp>("DFCHP03");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        foreach (var e in combatState.HittableEnemies.ToList())
            await CreatureCmd.GainMaxHp(e, 2500m);

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

        // 1. ArenaPreparation: Exhausts, adds 2 random Skills with Retain to Hand.
        await ResetCombatState();
        var arena = combatState.CreateCard<ArenaPreparation>(player);
        await CardPileCmd.Add(arena, PileType.Hand);
        AutoTestAssert.True(arena.TryManualPlay(null), "Play ArenaPreparation");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(PileType.Exhaust, arena.Pile?.Type, "ArenaPreparation exhausted");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "ArenaPreparation added 2 Skills to Hand");
        foreach (var added in pcs.Hand.Cards)
        {
            AutoTestAssert.Equal(CardType.Skill, added.Type, "ArenaPreparation generated Skill");
            AutoTestAssert.True(added.Keywords.Contains(CardKeyword.Retain), "ArenaPreparation granted Retain");
        }

        // 2. Backstep: 6 Block + Vigor(5) = 11 Block; does NOT consume Vigor!
        await ResetCombatState();
        await PowerCmd.Apply<VigorPower>(ctx, player.Creature, 5m, player.Creature, null);
        var backstep = combatState.CreateCard<Backstep>(player);
        await CardPileCmd.Add(backstep, PileType.Hand);
        AutoTestAssert.True(backstep.TryManualPlay(null), "Play Backstep");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(11, player.Creature.Block, "Backstep gained 6 + 5 Vigor = 11 Block");
        AutoTestAssert.Equal(5, player.Creature.GetPowerAmount<VigorPower>(), "Backstep did not consume Vigor");

        // 3. BerserkerStyle (Hone Blade) -> HonePower(3): boosts Strike (+3), not non-Strike!
        await ResetCombatState();
        var honeCard = combatState.CreateCard<BerserkerStyle>(player);
        await CardPileCmd.Add(honeCard, PileType.Hand);
        AutoTestAssert.True(honeCard.TryManualPlay(null), "Play BerserkerStyle");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<HonePower>(), "HonePower(3) applied");

        int hp = enemy.CurrentHp;
        var strikeHone = combatState.CreateCard<ChampStrike>(player);
        await CardPileCmd.Add(strikeHone, PileType.Hand);
        AutoTestAssert.True(strikeHone.TryManualPlay(enemy), "Play Strike with HonePower(3)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 9, enemy.CurrentHp, "HonePower boosted Strike damage (6 + 3 = 9)");

        hp = enemy.CurrentHp;
        var nonStrike = combatState.CreateCard<CrownThrow>(player);
        await CardPileCmd.Add(nonStrike, PileType.Hand);
        AutoTestAssert.True(nonStrike.TryManualPlay(enemy), "Play non-Strike CrownThrow with HonePower(3)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 8, enemy.CurrentHp, "HonePower did not boost non-Strike CrownThrow (8)");

        // 4. BladeFlurry (Flurry of Strikes) + DualPlayStrikePower: replays next Strike twice!
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var flurry = combatState.CreateCard<BladeFlurry>(player);
        await CardPileCmd.Add(flurry, PileType.Hand);
        AutoTestAssert.True(flurry.TryManualPlay(enemy), "Play BladeFlurry");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 6, enemy.CurrentHp, "BladeFlurry dealt 6 damage");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DualPlayStrikePower>(), "BladeFlurry applied DualPlayStrikePower(1)");

        hp = enemy.CurrentHp;
        var dualStrike = combatState.CreateCard<ChampStrike>(player);
        await CardPileCmd.Add(dualStrike, PileType.Hand);
        AutoTestAssert.True(dualStrike.TryManualPlay(enemy), "Play Strike with DualPlayStrikePower(1)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 12, enemy.CurrentHp, "DualPlayStrikePower played Strike twice (6x2 = 12)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DualPlayStrikePower>(), "DualPlayStrikePower consumed");

        // 5. ChampCalledShot (Refreshment): Defensive draws 3 & Discards; Berserker gains 2 Energy & Exhausts; Ultimate does BOTH & Exhausts!
        await ResetCombatState(energy: 5);
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<ChampStrike>(player), PileType.Draw);
        var csDef = combatState.CreateCard<ChampCalledShot>(player);
        await CardPileCmd.Add(csDef, PileType.Hand);
        AutoTestAssert.True(csDef.TryManualPlay(null), "Play ChampCalledShot in Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "ChampCalledShot Defensive Combo drew 3");
        AutoTestAssert.Equal(4, pcs.Energy, "ChampCalledShot Defensive did not gain Energy (5 - 1 = 4)");
        AutoTestAssert.Equal(PileType.Discard, csDef.Pile?.Type, "ChampCalledShot went to Discard in Defensive");

        await ResetCombatState(energy: 5);
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        var csBer = combatState.CreateCard<ChampCalledShot>(player);
        await CardPileCmd.Add(csBer, PileType.Hand);
        AutoTestAssert.True(csBer.TryManualPlay(null), "Play ChampCalledShot in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, pcs.Energy, "ChampCalledShot Berserker Combo gained 2 Energy (5 - 1 + 2 = 6)");
        AutoTestAssert.Equal(PileType.Exhaust, csBer.Pile?.Type, "ChampCalledShot exhausted in Berserker");

        await ResetCombatState(energy: 5);
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Ultimate, ctx);
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<ChampStrike>(player), PileType.Draw);
        var csUlt = combatState.CreateCard<ChampCalledShot>(player);
        await CardPileCmd.Add(csUlt, PileType.Hand);
        AutoTestAssert.True(csUlt.TryManualPlay(null), "Play ChampCalledShot in Ultimate");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, pcs.Energy, "ChampCalledShot Ultimate gained 2 Energy");
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "ChampCalledShot Ultimate drew 3 cards");
        AutoTestAssert.Equal(PileType.Exhaust, csUlt.Pile?.Type, "ChampCalledShot exhausted in Ultimate");

        // 6. CrookedStrike (Finisher + Strike): uses Vigor WITHOUT consuming it! Then normal Strike consumes Vigor!
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        await PowerCmd.Apply<VigorPower>(ctx, player.Creature, 7m, player.Creature, null);
        hp = enemy.CurrentHp;
        var crooked = combatState.CreateCard<CrookedStrike>(player);
        await CardPileCmd.Add(crooked, PileType.Hand);
        AutoTestAssert.True(crooked.TryManualPlay(enemy), "Play CrookedStrike in Berserker with 7 Vigor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 13, enemy.CurrentHp, "CrookedStrike dealt 6 + 7 Vigor = 13 damage");
        AutoTestAssert.Equal(7, player.Creature.GetPowerAmount<VigorPower>(), "CrookedStrike did NOT consume Vigor!");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "CrookedStrike triggered Berserker Finisher (+1 Strength)");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "CrookedStrike exited Stance");

        hp = enemy.CurrentHp;
        var normalStrike = combatState.CreateCard<ChampStrike>(player);
        await CardPileCmd.Add(normalStrike, PileType.Hand);
        AutoTestAssert.True(normalStrike.TryManualPlay(enemy), "Play normal Strike to consume preserved Vigor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 14, enemy.CurrentHp, "Normal Strike used preserved 7 Vigor + 1 Strength (6 + 7 + 1 = 14)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<VigorPower>(), "Normal Strike consumed Vigor");

        // 7. DeathBlow: 12 AOE dmg + 8 Vigor + Exhausts.
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var deathBlow = combatState.CreateCard<DeathBlow>(player);
        await CardPileCmd.Add(deathBlow, PileType.Hand);
        AutoTestAssert.True(deathBlow.TryManualPlay(null), "Play DeathBlow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 12, enemy.CurrentHp, "DeathBlow dealt 12 AOE damage");
        AutoTestAssert.Equal(8, player.Creature.GetPowerAmount<VigorPower>(), "DeathBlow granted 8 Vigor");
        AutoTestAssert.Equal(PileType.Exhaust, deathBlow.Pile?.Type, "DeathBlow exhausted");

        // 8. DefensiveStyle (Double Style) -> DoubleStylePower(1): Skill Bonuses grant +1 Vigor & +1 Counter!
        await ResetCombatState();
        var doubleStyle = combatState.CreateCard<DefensiveStyle>(player);
        await CardPileCmd.Add(doubleStyle, PileType.Hand);
        AutoTestAssert.True(doubleStyle.TryManualPlay(null), "Play DefensiveStyle (Double Style)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DoubleStylePower>(), "DoubleStylePower(1) applied");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Ultimate, ctx);
        var defSkill = combatState.CreateCard<ChampDefend>(player);
        await CardPileCmd.Add(defSkill, PileType.Hand);
        AutoTestAssert.True(defSkill.TryManualPlay(null), "Play Defend in Ultimate with DoubleStylePower(1)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<VigorPower>(), "DoubleStylePower boosted Berserker Skill Bonus (2+1 = 3 Vigor)");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<CounterPower>(), "DoubleStylePower boosted Defensive Skill Bonus (3+1 = 4 Counter)");

        // 9. Duel (Challenge): 8 Block + 8 dmg once vs 0-Str enemy; twice (16 Block + 16 dmg) vs >0-Str enemy!
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var duel1 = combatState.CreateCard<Duel>(player);
        await CardPileCmd.Add(duel1, PileType.Hand);
        AutoTestAssert.True(duel1.TryManualPlay(enemy), "Play Duel vs 0-Strength enemy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.Block, "Duel gained 8 Block once");
        AutoTestAssert.Equal(hp - 8, enemy.CurrentHp, "Duel dealt 8 damage once");

        await ResetCombatState();
        await PowerCmd.Apply<StrengthPower>(ctx, enemy, 2m, enemy, null);
        hp = enemy.CurrentHp;
        var duel2 = combatState.CreateCard<Duel>(player);
        await CardPileCmd.Add(duel2, PileType.Hand);
        AutoTestAssert.True(duel2.TryManualPlay(enemy), "Play Duel vs 2-Strength enemy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(16, player.Creature.Block, "Duel repeated Block vs Strength enemy (8x2 = 16)");
        AutoTestAssert.Equal(hp - 16, enemy.CurrentHp, "Duel repeated damage vs Strength enemy (8x2 = 16)");

        // 10. EnragedBash: Berserker Combo permanently increments hit count this combat!
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        var enraged = combatState.CreateCard<EnragedBash>(player);
        await CardPileCmd.Add(enraged, PileType.Hand);
        hp = enemy.CurrentHp;
        AutoTestAssert.True(enraged.TryManualPlay(enemy), "Play EnragedBash #1 in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 7, enemy.CurrentHp, "EnragedBash #1 dealt 7 (1 hit)");
        AutoTestAssert.Equal(2, enraged.DynamicVars.Repeat.IntValue, "EnragedBash hit count incremented to 2");

        await CardPileCmd.Add(enraged, PileType.Hand);
        string enragedDesc2 = enraged.GetDescriptionForPile(PileType.Hand);
        AutoTestAssert.True(enragedDesc2.Contains("2 times", StringComparison.Ordinal) && !enragedDesc2.Contains('{') && !enragedDesc2.Contains('}'), $"EnragedBash Repeat=2 loc rendered cleanly: {enragedDesc2}");
        hp = enemy.CurrentHp;
        AutoTestAssert.True(enraged.TryManualPlay(enemy), "Play EnragedBash #2 in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 14, enemy.CurrentHp, "EnragedBash #2 dealt 7x2 = 14 damage");
        AutoTestAssert.Equal(3, enraged.DynamicVars.Repeat.IntValue, "EnragedBash hit count incremented to 3");

        // 11. FaceSlap: Neutral 8 dmg + 0 Vuln; Berserker 8 dmg + 2 Vuln.
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var slapNeutral = combatState.CreateCard<FaceSlap>(player);
        await CardPileCmd.Add(slapNeutral, PileType.Hand);
        AutoTestAssert.True(slapNeutral.TryManualPlay(enemy), "Play FaceSlap outside Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 8, enemy.CurrentHp, "FaceSlap dealt 8 damage");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<VulnerablePower>(), "FaceSlap applied 0 Vuln outside Berserker");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        var slapBer = combatState.CreateCard<FaceSlap>(player);
        await CardPileCmd.Add(slapBer, PileType.Hand);
        AutoTestAssert.True(slapBer.TryManualPlay(enemy), "Play FaceSlap inside Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "FaceSlap Berserker Combo applied 2 Vulnerable");

        // 12. FanOfKnives: Neutral 4 AOE dmg; Berserker 4x2 = 8 AOE dmg!
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var knives1 = combatState.CreateCard<FanOfKnives>(player);
        await CardPileCmd.Add(knives1, PileType.Hand);
        AutoTestAssert.True(knives1.TryManualPlay(null), "Play FanOfKnives outside Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 4, enemy.CurrentHp, "FanOfKnives dealt 4 damage outside Berserker");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        hp = enemy.CurrentHp;
        var knives2 = combatState.CreateCard<FanOfKnives>(player);
        await CardPileCmd.Add(knives2, PileType.Hand);
        AutoTestAssert.True(knives2.TryManualPlay(null), "Play FanOfKnives inside Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 8, enemy.CurrentHp, "FanOfKnives Berserker Combo dealt 4x2 = 8 damage");

        // 13. FancyFootwork + FocusedBerPower + FocusedBerserking (All Out) + GladiatorStyle (Good Clean Fight)!
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        var fancy = combatState.CreateCard<FancyFootwork>(player);
        await CardPileCmd.Add(fancy, PileType.Hand);
        AutoTestAssert.True(fancy.TryManualPlay(null), "Play FancyFootwork from Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(ChampStanceKind.Berserker, ChampStances.GetCurrentStance(player.Creature), "FancyFootwork switched Defensive -> Berserker");
        AutoTestAssert.Equal(10, player.Creature.GetPowerAmount<FocusedBerPower>(), "FancyFootwork applied FocusedBerPower(10)");

        // Play FocusedBerserking (All Out, Finisher): triggers Berserker Finisher TWICE (+2 Str) WITHOUT exiting Stance, AND triggers FocusedBerPower (10 AOE damage)!
        hp = enemy.CurrentHp;
        var allOut = combatState.CreateCard<FocusedBerserking>(player);
        await CardPileCmd.Add(allOut, PileType.Hand);
        AutoTestAssert.True(allOut.TryManualPlay(null), "Play FocusedBerserking in Berserker with FocusedBerPower(10)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "FocusedBerserking triggered Berserker Finisher twice (+2 Strength)");
        AutoTestAssert.Equal(ChampStanceKind.Berserker, ChampStances.GetCurrentStance(player.Creature), "FocusedBerserking did NOT exit Berserker Stance");
        AutoTestAssert.Equal(PileType.Exhaust, allOut.Pile?.Type, "FocusedBerserking exhausted");
        AutoTestAssert.Equal(hp - 10, enemy.CurrentHp, "FocusedBerPower dealt 10 AOE damage on Finisher");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<FocusedBerPower>(), "FocusedBerPower consumed after Finisher");

        // GladiatorStyle in Ultimate -> +2 Strength AND +2 Dexterity!
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Ultimate, ctx);
        var glad = combatState.CreateCard<GladiatorStyle>(player);
        await CardPileCmd.Add(glad, PileType.Hand);
        AutoTestAssert.True(glad.TryManualPlay(null), "Play GladiatorStyle in Ultimate");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "GladiatorStyle Berserker Combo granted +2 Strength");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DexterityPower>(), "GladiatorStyle Defensive Combo granted +2 Dexterity");

        // 14. FalseCounter (Rope-a-dope): Finisher -> 8 Block + 6 Finisher Block + DrawNextTurn(2) + EnergyNextTurn(1) + exits Stance -> verified on Turn 2!
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        var rope = combatState.CreateCard<FalseCounter>(player);
        await CardPileCmd.Add(rope, PileType.Hand);
        AutoTestAssert.True(rope.TryManualPlay(null), "Play FalseCounter in Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(14, player.Creature.Block, "FalseCounter gained 8 + 6 Defensive Finisher = 14 Block");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "FalseCounter exited Stance");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "FalseCounter applied DrawCardsNextTurnPower(2)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnergyNextTurnPower>(), "FalseCounter applied EnergyNextTurnPower(1)");

        // Also verify unused FocusedBerPower & DualPlayStrikePower expire at end of Turn 1!
        await PowerCmd.Apply<FocusedBerPower>(ctx, player.Creature, 5m, player.Creature, null);
        await PowerCmd.Apply<DualPlayStrikePower>(ctx, player.Creature, 1m, player.Creature, null);
        if (player.Creature.GetPower<CounterPower>() is { } fcCounter)
            await PowerCmd.Remove(fcCounter);
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<FocusedBerPower>(), "Unused FocusedBerPower expired at end of Turn 1");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DualPlayStrikePower>(), "Unused DualPlayStrikePower expired at end of Turn 1");
        AutoTestAssert.Equal(4, pcs.Energy, "Turn 2 started with 3 + 1 = 4 Energy from FalseCounter");
        AutoTestAssert.Equal(7, pcs.Hand.Cards.Count, "Turn 2 started with 5 + 2 = 7 cards drawn from FalseCounter");
    }

    public static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheChamp>("DFCHP04");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        foreach (var e in combatState.HittableEnemies.ToList())
            await CreatureCmd.GainMaxHp(e, 2500m);

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

        // 1. GutPunch: Neutral -> 8 dmg + stays Neutral; In Stance -> 8 dmg + Ultimate Stance + UltimateFormPower(1) + Exhaust!
        await ResetCombatState();
        int hp = enemy.CurrentHp;
        var gutNeutral = combatState.CreateCard<GutPunch>(player);
        await CardPileCmd.Add(gutNeutral, PileType.Hand);
        AutoTestAssert.True(gutNeutral.TryManualPlay(enemy), "Play GutPunch in Neutral");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 8, enemy.CurrentHp, "GutPunch dealt 8 damage");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "GutPunch Neutral did not enter Ultimate");
        AutoTestAssert.Equal(PileType.Exhaust, gutNeutral.Pile?.Type, "GutPunch exhausted");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        var gutStance = combatState.CreateCard<GutPunch>(player);
        await CardPileCmd.Add(gutStance, PileType.Hand);
        AutoTestAssert.True(gutStance.TryManualPlay(enemy), "Play GutPunch in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(ChampStanceKind.Ultimate, ChampStances.GetCurrentStance(player.Creature), "GutPunch Combo entered Ultimate Stance");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<UltimateFormPower>(), "GutPunch Combo applied UltimateFormPower(1)");

        // 2. IronFortress (Technical Jig) -> IronFortressPower(3): entering a new Stance grants 3 Block; exiting/same-stance grants 0!
        await ResetCombatState();
        var jig = combatState.CreateCard<IronFortress>(player);
        await CardPileCmd.Add(jig, PileType.Hand);
        AutoTestAssert.True(jig.TryManualPlay(null), "Play IronFortress (Technical Jig)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<IronFortressPower>(), "IronFortressPower(3) applied");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        AutoTestAssert.Equal(3, player.Creature.Block, "IronFortressPower granted 3 Block on entering Berserker");
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        AutoTestAssert.Equal(3, player.Creature.Block, "IronFortressPower did not trigger on same Stance");
        await ChampStances.ExitStanceAsync(player, ctx);
        AutoTestAssert.Equal(3, player.Creature.Block, "IronFortressPower did not trigger on exiting to Neutral");

        // 3. Lariat (X-cost): at 3 Energy in Berserker -> 5*3 = 15 Block + 3 Skill Bonuses (+6 Vigor) + 1 Skill charge (+2 Vigor) = 8 Vigor!
        await ResetCombatState(energy: 3);
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        var lariat = combatState.CreateCard<Lariat>(player);
        await CardPileCmd.Add(lariat, PileType.Hand);
        AutoTestAssert.True(lariat.TryManualPlay(null), "Play Lariat at X=3 in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, pcs.Energy, "Lariat spent all 3 Energy");
        AutoTestAssert.Equal(15, player.Creature.Block, "Lariat gained 5 * 3 = 15 Block");
        AutoTestAssert.Equal(8, player.Creature.GetPowerAmount<VigorPower>(), "Lariat triggered Berserker Skill Bonus 3 times (+6) plus 1 charge (+2) = 8 Vigor");

        // 4. Parry + ParryPower: 8 Block + 4 Counter + ParryPower(1) -> attacked adds 2 Ripostes (each 4 dmg) & consumes ParryPower!
        await ResetCombatState();
        var parry = combatState.CreateCard<Parry>(player);
        await CardPileCmd.Add(parry, PileType.Hand);
        AutoTestAssert.True(parry.TryManualPlay(null), "Play Parry");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.Block, "Parry gained 8 Block");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<CounterPower>(), "Parry gained 4 Counter");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<Downfall.Champ.Powers.ParryPower>(), "Parry applied ParryPower(1)");

        await CreatureCmd.Damage(ctx, player.Creature, 5m, ValueProp.Move, enemy, null, null);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<CounterPower>(), "CounterPower consumed");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<Downfall.Champ.Powers.ParryPower>(), "ParryPower consumed");
        var ripostes = pcs.Hand.Cards.OfType<Riposte>().ToList();
        AutoTestAssert.Equal(2, ripostes.Count, "ParryPower doubled Riposte Strike generation (2 Ripostes)");
        AutoTestAssert.True(ripostes.All(r => r.DynamicVars.Damage.BaseValue == 4m), "Both Ripostes deal 4 base damage");

        // 5. PreemptiveStrike: deals Counter damage AOE; loses half Counter outside Defensive, keeps all Counter inside Defensive!
        await ResetCombatState();
        await PowerCmd.Apply<CounterPower>(ctx, player.Creature, 10m, player.Creature, null);
        hp = enemy.CurrentHp;
        var preemptNeutral = combatState.CreateCard<PreemptiveStrike>(player);
        await CardPileCmd.Add(preemptNeutral, PileType.Hand);
        AutoTestAssert.True(preemptNeutral.TryManualPlay(null), "Play PreemptiveStrike outside Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 10, enemy.CurrentHp, "PreemptiveStrike dealt 10 Counter damage");
        AutoTestAssert.Equal(5, player.Creature.GetPowerAmount<CounterPower>(), "PreemptiveStrike halved Counter outside Defensive (10 -> 5)");

        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        hp = enemy.CurrentHp;
        var preemptDef = combatState.CreateCard<PreemptiveStrike>(player);
        await CardPileCmd.Add(preemptDef, PileType.Hand);
        AutoTestAssert.True(preemptDef.TryManualPlay(null), "Play PreemptiveStrike inside Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 5, enemy.CurrentHp, "PreemptiveStrike dealt 5 Counter damage");
        AutoTestAssert.Equal(5, player.Creature.GetPowerAmount<CounterPower>(), "PreemptiveStrike Defensive Combo kept all 5 Counter");

        // 6. RageSigil (Battle Plan): enters Defensive, 2 Block, Scry 2!
        await ResetCombatState();
        for (int i = 0; i < 3; i++)
            await CardPileCmd.Add(combatState.CreateCard<ChampStrike>(player), PileType.Draw);
        var battlePlan = combatState.CreateCard<RageSigil>(player);
        await CardPileCmd.Add(battlePlan, PileType.Hand);
        AutoTestAssert.True(battlePlan.TryManualPlay(null), "Play RageSigil (Battle Plan)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(ChampStanceKind.Defensive, ChampStances.GetCurrentStance(player.Creature), "RageSigil entered Defensive");
        AutoTestAssert.Equal(2, player.Creature.Block, "RageSigil gained 2 Block");

        // 7. RecklessLeap (Reckless Strike): enters Berserker, 6 dmg, +1 Strength; base Exhausts, Upgraded Discards!
        await ResetCombatState();
        hp = enemy.CurrentHp;
        var recklessBase = combatState.CreateCard<RecklessLeap>(player);
        await CardPileCmd.Add(recklessBase, PileType.Hand);
        AutoTestAssert.True(recklessBase.TryManualPlay(enemy), "Play base RecklessLeap");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(ChampStanceKind.Berserker, ChampStances.GetCurrentStance(player.Creature), "RecklessLeap entered Berserker");
        AutoTestAssert.Equal(hp - 6, enemy.CurrentHp, "RecklessLeap dealt 6 damage");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "RecklessLeap granted +1 Strength");
        AutoTestAssert.Equal(PileType.Exhaust, recklessBase.Pile?.Type, "Base RecklessLeap exhausted");

        var recklessPlus = combatState.CreateCard<RecklessLeap>(player);
        CardCmd.Upgrade(recklessPlus);
        await CardPileCmd.Add(recklessPlus, PileType.Hand);
        AutoTestAssert.True(recklessPlus.TryManualPlay(enemy), "Play upgraded RecklessLeap+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(PileType.Discard, recklessPlus.Pile?.Type, "Upgraded RecklessLeap+ went to Discard instead of Exhaust");

        // 8. SetATrap: 8 Block + Defensive Combo Weak to ALL enemies.
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        var trap = combatState.CreateCard<SetATrap>(player);
        await CardPileCmd.Add(trap, PileType.Hand);
        AutoTestAssert.True(trap.TryManualPlay(null), "Play SetATrap in Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.Block, "SetATrap gained 8 Block");
        foreach (var e in combatState.HittableEnemies)
            AutoTestAssert.Equal(1, e.GetPowerAmount<WeakPower>(), "SetATrap Defensive Combo applied 1 Weak to all enemies");

        // 9. Shatter: 14 dmg + Any Stance Combo applies 1 Weak & 1 Vulnerable.
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        hp = enemy.CurrentHp;
        var shatter = combatState.CreateCard<Shatter>(player);
        await CardPileCmd.Add(shatter, PileType.Hand);
        AutoTestAssert.True(shatter.TryManualPlay(enemy), "Play Shatter in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hp - 14, enemy.CurrentHp, "Shatter dealt 14 damage");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "Shatter Combo applied 1 Weak");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "Shatter Combo applied 1 Vulnerable");

        // 10. ShieldSlam (Moment of Truth): Retain + Finisher -> draws 1 card + triggers Finisher Bonus + exits Stance!
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        await CardPileCmd.Add(combatState.CreateCard<ChampStrike>(player), PileType.Draw);
        var truth = combatState.CreateCard<ShieldSlam>(player);
        AutoTestAssert.True(truth.Keywords.Contains(CardKeyword.Retain), "ShieldSlam has Retain");
        await CardPileCmd.Add(truth, PileType.Hand);
        AutoTestAssert.True(truth.TryManualPlay(null), "Play ShieldSlam in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "ShieldSlam drew 1 card");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "ShieldSlam triggered Berserker Finisher (+1 Strength)");
        AutoTestAssert.Equal(ChampStanceKind.None, ChampStances.GetCurrentStance(player.Creature), "ShieldSlam exited Stance");

        // 11. SkillfulDodge: Defensive Combo permanently scales its Block & Counter by +3 this combat!
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Defensive, ctx);
        var dodge = combatState.CreateCard<SkillfulDodge>(player);
        await CardPileCmd.Add(dodge, PileType.Hand);
        AutoTestAssert.True(dodge.TryManualPlay(null), "Play SkillfulDodge #1 in Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.Block, "SkillfulDodge #1 gained 4 Block");
        AutoTestAssert.Equal(7, player.Creature.GetPowerAmount<CounterPower>(), "SkillfulDodge #1 gained 4 + 3 (Skill Bonus) = 7 Counter");
        AutoTestAssert.Equal(7m, dodge.DynamicVars.Block.BaseValue, "SkillfulDodge Block scaled to 7");
        AutoTestAssert.Equal(7m, dodge.DynamicVars[nameof(CounterPower)].BaseValue, "SkillfulDodge Counter scaled to 7");

        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await CardPileCmd.Add(dodge, PileType.Hand);
        AutoTestAssert.True(dodge.TryManualPlay(null), "Play SkillfulDodge #2 in Defensive");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.Block, "SkillfulDodge #2 gained 7 Block");

        // 12. SwordSigil (Sigil of Victory) & ViciousMockery: trigger Skill Bonus!
        await ResetCombatState();
        await ChampStances.EnterStanceAsync(player, ChampStanceKind.Berserker, ctx);
        var swordSigil = combatState.CreateCard<SwordSigil>(player);
        await CardPileCmd.Add(swordSigil, PileType.Hand);
        AutoTestAssert.True(swordSigil.TryManualPlay(null), "Play SwordSigil in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        // 1 charge (+2 Vigor) + 3 triggers (+6 Vigor) = 8 Vigor!
        AutoTestAssert.Equal(8, player.Creature.GetPowerAmount<VigorPower>(), "SwordSigil triggered Berserker Skill Bonus 3 times (+6) + 1 charge (+2) = 8 Vigor");

        var mockery = combatState.CreateCard<ViciousMockery>(player);
        await CardPileCmd.Add(mockery, PileType.Hand);
        AutoTestAssert.True(mockery.TryManualPlay(enemy), "Play ViciousMockery in Berserker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        // +3 own Vigor + 1 charge (+2) + 1 explicit Skill Bonus (+2) = +7 Vigor -> 15 total!
        AutoTestAssert.Equal(15, player.Creature.GetPowerAmount<VigorPower>(), "ViciousMockery granted +3 Vigor + 1 charge (+2) + 1 Skill Bonus (+2) = 15 Vigor");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "ViciousMockery applied 1 Weak");

        // 13. WindUp: chooses Stance AND fetches a Finisher from Draw Pile to Hand! Base Exhausts, Upgraded Discards!
        await ResetCombatState();
        var fetchedFinisher = combatState.CreateCard<Execute>(player);
        await CardPileCmd.Add(fetchedFinisher, PileType.Draw);
        var windUpBase = combatState.CreateCard<WindUp>(player);
        await CardPileCmd.Add(windUpBase, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is ChooseDefensiveStance || ReferenceEquals(c, fetchedFinisher)))
        {
            AutoTestAssert.True(windUpBase.TryManualPlay(null), "Play WindUp");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(ChampStanceKind.Defensive, ChampStances.GetCurrentStance(player.Creature), "WindUp entered Defensive Stance");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(fetchedFinisher), "WindUp fetched Execute from Draw Pile into Hand");
        AutoTestAssert.Equal(PileType.Exhaust, windUpBase.Pile?.Type, "Base WindUp exhausted");

        var windUpPlus = combatState.CreateCard<WindUp>(player);
        CardCmd.Upgrade(windUpPlus);
        await CardPileCmd.Add(windUpPlus, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is ChooseBerserkerStance))
        {
            AutoTestAssert.True(windUpPlus.TryManualPlay(null), "Play upgraded WindUp+");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(PileType.Discard, windUpPlus.Pile?.Type, "Upgraded WindUp+ went to Discard instead of Exhaust");

        // 14. ShieldWall (Iron Fortress) -> Dexterity(2) + ShieldWallBlockPower(3) (non-decaying!) + unused ParryPower expiry!
        await ResetCombatState();
        var wall = combatState.CreateCard<ShieldWall>(player);
        await CardPileCmd.Add(wall, PileType.Hand);
        AutoTestAssert.True(wall.TryManualPlay(null), "Play ShieldWall (Iron Fortress)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DexterityPower>(), "ShieldWall granted +2 Dexterity");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<ShieldWallBlockPower>(), "ShieldWall applied ShieldWallBlockPower(3)");

        await PowerCmd.Apply<Downfall.Champ.Powers.ParryPower>(ctx, player.Creature, 1m, player.Creature, null);
        // Take unblocked damage before ending turn to prove ShieldWallBlockPower never decays like PlatingPower!
        await CreatureCmd.Damage(ctx, player.Creature, 1m, ValueProp.Unpowered, enemy, null, null);
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<ShieldWallBlockPower>(), "ShieldWallBlockPower did NOT decay across turns or damage");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<Downfall.Champ.Powers.ParryPower>(), "Unused ParryPower expired at end of round");
    }
}
