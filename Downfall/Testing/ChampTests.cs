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
}
