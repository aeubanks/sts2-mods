using Downfall.Core;
using Downfall.Slimebound;
using Downfall.Slimebound.Cards;
using Downfall.Slimebound.Orbs;
using Downfall.Slimebound.Powers;
using Downfall.Slimebound.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Testing;

internal static class SlimeboundTests
{
    internal static async Task TestVisualsRuntime()
    {
        await DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheSlimeBoss>("SLIME_VIS01");
        DownfallAutoTestRunner.CleanupRun();

        // Also verify live NOrb + DownfallOrbPatch rendering in combat SceneTree!
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSlimeBoss>("SLIME_ORBVIS");
        var ctx = new ThrowingPlayerChoiceContext();
        await SlimeMechanics.SplitSlimeAsync<AttackSlime>(ctx, player);
        await SlimeMechanics.SplitSlimeAsync<ShieldSlime>(ctx, player);
        await SlimeMechanics.SplitSlimeAsync<SlimingSlime>(ctx, player);

        var creatureNode = AutoTestAssert.NotNull(NCombatRoom.Instance?.GetCreatureNode(player.Creature), "Slime Boss NCreature");
        var orbManager = AutoTestAssert.NotNull(creatureNode.OrbManager, "Slime Boss NOrbManager");
        var orbNodes = new List<NOrb>();
        DownfallAutoTestRunner.CollectNodes(orbManager, orbNodes);
        AutoTestAssert.Equal(3, orbNodes.Count, "Slime Boss rendered 3 live NOrb nodes");
        AutoTestAssert.True(orbNodes.All(o => o.Model is SlimeOrb), "All 3 NOrb nodes hold SlimeOrbs");
    }

    internal static async Task TestStarterCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSlimeBoss>("SLIME_START01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "Slime Boss PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();
        await CreatureCmd.GainMaxHp(enemy1, 400m);
        await CreatureCmd.GainMaxHp(enemy2, 400m);

        // 1. Starter stats, 3 Orb slots, 10-card deck, Heart of Goo starter relic.
        AutoTestAssert.Equal(65, player.Creature.MaxHp, "Slime Boss StartingHp is 65");
        AutoTestAssert.Equal(3, pcs.OrbQueue.Capacity, "Slime Boss starts with 3 Slime slots");
        AutoTestAssert.Equal(10, player.Deck.Cards.Count, "Slime Boss starter deck has 10 cards");
        var heart = AutoTestAssert.NotNull(player.Relics.OfType<AbsorbEndCombat>().FirstOrDefault(), "Starts with Heart of Goo");
        AutoTestAssert.Equal(8, heart.DisplayAmount, "Heart of Goo starts combat with 8 heal cap");

        // 2. CorrosiveSpit (6 Goop) + Split (Guerilla Slime AoE 3 Unpowered -> must NOT consume Goop!) + Strike (6 + 6 = 12 dmg, consumes Goop, heals +2 HP!).
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.Damage(ctx, player.Creature, 20m, ValueProp.Unblockable | ValueProp.Unpowered, player.Creature);
        int hpBefore = player.Creature.CurrentHp;

        var spit = combatState.CreateCard<CorrosiveSpit>(player);
        var split = combatState.CreateCard<Split>(player);
        var strike = combatState.CreateCard<SlimeboundStrike>(player);
        await CardPileCmd.Add(spit, PileType.Hand);
        await CardPileCmd.Add(split, PileType.Hand);
        await CardPileCmd.Add(strike, PileType.Hand);

        AutoTestAssert.True(spit.TryManualPlay(enemy1), "Play CorrosiveSpit");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, enemy1.GetPowerAmount<SlimedPower>(), "CorrosiveSpit applied 6 Goop");

        int e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(split.TryManualPlay(null), "Play Split");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, pcs.OrbQueue.Orbs.Count, "Split spawned 1 Guerilla Slime");
        AutoTestAssert.True(pcs.OrbQueue.Orbs[0] is PoisonSlime, "Split spawned PoisonSlime");
        AutoTestAssert.Equal(e1Hp - 3, enemy1.CurrentHp, "Split Command dealt 3 AoE to enemy 1");
        AutoTestAssert.Equal(e2Hp - 3, enemy2.CurrentHp, "Split Command dealt 3 AoE to enemy 2");
        AutoTestAssert.Equal(6, enemy1.GetPowerAmount<SlimedPower>(), "Unpowered Slime attack did NOT consume Goop");

        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(strike.TryManualPlay(enemy1), "Play SlimeboundStrike on Gooped enemy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 12, enemy1.CurrentHp, "Strike dealt 6 + 6 Goop = 12 damage");
        AutoTestAssert.Equal(0, enemy1.GetPowerAmount<SlimedPower>(), "Strike consumed Goop");
        AutoTestAssert.Equal(hpBefore + 2, player.Creature.CurrentHp, "Heart of Goo healed +2 HP on Goop consume");
        AutoTestAssert.Equal(6, heart.DisplayAmount, "Heart of Goo cap decremented 8 -> 6");

        // 3. Tackle: 13 dmg + 3 self-damage!
        await ClearHandAndRefillEnergy(player, pcs);
        var tackle = combatState.CreateCard<Tackle>(player);
        await CardPileCmd.Add(tackle, PileType.Hand);
        hpBefore = player.Creature.CurrentHp;
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(tackle.TryManualPlay(enemy1), "Play Tackle");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 13, enemy1.CurrentHp, "Tackle dealt 13 damage");
        AutoTestAssert.Equal(hpBefore - 3, player.Creature.CurrentHp, "Tackle dealt 3 self-damage");

        // 4. Lick+ token (0 cost, Exhaust, 6 Goop)!
        var lick = combatState.CreateCard<Lick>(player);
        CardCmd.Upgrade(lick);
        await CardPileCmd.Add(lick, PileType.Hand);
        AutoTestAssert.True(lick.TryManualPlay(enemy1), "Play Lick+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, enemy1.GetPowerAmount<SlimedPower>(), "Lick+ applied 6 Goop");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(lick), "Lick+ exhausted");
        await PowerCmd.Remove<SlimedPower>(enemy1);

        // 5. PotencyPower (+3) scales Slimes while Defect's FocusPower (+5) does NOT!
        // Also verify Leading Slime = NEWEST (LastOrDefault), and Absorb evokes specific Slime without touching Defect orb!
        await SlimeMechanics.AbsorbAllSlimesAsync(ctx, player);
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Apply<FocusPower>(ctx, player.Creature, 5m, player.Creature, null);
        await PowerCmd.Apply<PotencyPower>(ctx, player.Creature, 3m, player.Creature, null);

        await OrbCmd.Channel<LightningOrb>(ctx, player); // Slot 0: Defect orb
        var shieldSlime = AutoTestAssert.NotNull(await SlimeMechanics.SplitSlimeAsync<ShieldSlime>(ctx, player), "Split ShieldSlime");
        var mireSlime = AutoTestAssert.NotNull(await SlimeMechanics.SplitSlimeAsync<SlimingSlime>(ctx, player), "Split SlimingSlime");
        AutoTestAssert.Equal(4m, shieldSlime.PassiveVal, "ShieldSlime Passive = 1 + 3 Potency = 4 (Focus ignored!)");
        AutoTestAssert.Equal(5m, shieldSlime.EvokeVal, "ShieldSlime Secondary = 3 + ceil(3/2) = 5 Block");
        AutoTestAssert.Equal(5m, mireSlime.PassiveVal, "SlimingSlime Passive = 2 + 3 Potency = 5 (Focus ignored!)");
        AutoTestAssert.Equal(4m, mireSlime.EvokeVal, "SlimingSlime Secondary = 2 + ceil(3/2) = 4 Goop");

        // Leading slime must be SlimingSlime (newest!), NOT LightningOrb or ShieldSlime!
        AutoTestAssert.Equal(mireSlime, SlimeMechanics.GetLeadingSlime(player), "Leading Slime is newest (SlimingSlime)");
        await SlimeMechanics.CommandAsync(ctx, player);
        int totalGoop = enemy1.GetPowerAmount<SlimedPower>() + enemy2.GetPowerAmount<SlimedPower>();
        AutoTestAssert.Equal(4, totalGoop, "Command triggered newest SlimingSlime (4 Goop)");

        // AbsorbLeadingSlimeAsync removes SlimingSlime (+1 Strength) and preserves LightningOrb in slot 0!
        AutoTestAssert.True(await SlimeMechanics.AbsorbLeadingSlimeAsync(ctx, player), "Absorbed leading SlimingSlime");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "Absorb granted +1 Strength");
        AutoTestAssert.True(pcs.OrbQueue.Orbs[0] is LightningOrb, "AbsorbLeadingSlimeAsync did NOT evoke Defect LightningOrb in slot 0");
        await PowerCmd.Remove<FocusPower>(player.Creature);
        await PowerCmd.Remove<PotencyPower>(player.Creature);

        // 6. Full-slot channel auto-evokes oldest orb; Touch of Orobas upgrades AbsorbEndCombat -> AbsorbEndCombatUpgraded (Starter rarity, 3/15)!
        var orobas = (MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas)ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.TouchOfOrobas>().ToMutable();
        AutoTestAssert.True(orobas.SetupForPlayer(player), "TouchOfOrobas found Slime Boss starter Heart of Goo");
        AutoTestAssert.Equal(ModelDb.Relic<AbsorbEndCombatUpgraded>().Id, orobas.UpgradedRelic, "TouchOfOrobas upgrades Heart of Goo -> Black Heart of Goo");
        await RelicCmd.Obtain(orobas, player);
        var blackHeart = AutoTestAssert.NotNull(player.Relics.OfType<AbsorbEndCombatUpgraded>().FirstOrDefault(), "Obtained Black Heart of Goo");
        AutoTestAssert.True(!player.Relics.OfType<AbsorbEndCombat>().Any(), "Original Heart of Goo replaced");
        await blackHeart.BeforeCombatStart();
        AutoTestAssert.Equal(15, blackHeart.DisplayAmount, "Black Heart of Goo cap is 15");

        // 7. SoulburnPower: 3-turn countdown (3 -> 2 -> 1 -> explode on enemy turn start) + AfterTurnStartOrbTrigger (ShieldSlime grants +3 Block at start of Turn 2)!
        var sb = await PowerCmd.Apply<SoulburnPower>(ctx, enemy1, 10m, player.Creature, null);
        AutoTestAssert.NotNull(sb, "Applied SoulburnPower(10)");
        AutoTestAssert.Equal(3, sb!.TurnsRemaining, "Soulburn starts at 3 turns");
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(2, sb.TurnsRemaining, "Soulburn decremented 3 -> 2 on Turn 1 enemy start");
        AutoTestAssert.Equal(3, player.Creature.Block, "ShieldSlime AfterTurnStartOrbTrigger granted +3 Block at start of Round 2");
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(1, sb.TurnsRemaining, "Soulburn decremented 2 -> 1 on Turn 2 enemy start");
        AutoTestAssert.True(sb.GetHealthBarForecastSegments(new BaseLib.Hooks.HealthBarForecastContext(enemy1)).Any(),
            "Soulburn emits health bar forecast when TurnsRemaining == 1");
        e1Hp = enemy1.CurrentHp;
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 4);
        AutoTestAssert.Equal(0, enemy1.GetPowerAmount<SoulburnPower>(), "Soulburn removed after detonating on Turn 3 enemy start");
        AutoTestAssert.True(enemy1.CurrentHp <= e1Hp - 10, $"Soulburn detonated for 10 unblockable HP loss (was {e1Hp}, now {enemy1.CurrentHp})");

        // 8. Heart of Goo / Black Heart of Goo heals even on LETHAL killing blow on Gooped enemy!
        if (player.Creature.CurrentHp > 30)
            await CreatureCmd.Damage(ctx, player.Creature, 20m, ValueProp.Unblockable | ValueProp.Unpowered, player.Creature);
        await SlimeMechanics.ApplyGoopAsync(ctx, enemy2, player, 10, null);
        await CreatureCmd.Damage(ctx, enemy2, enemy2.CurrentHp - 5, ValueProp.Unblockable | ValueProp.Unpowered, player.Creature);
        hpBefore = player.Creature.CurrentHp;
        await ClearHandAndRefillEnergy(player, pcs);
        var finishStrike = combatState.CreateCard<SlimeboundStrike>(player);
        await CardPileCmd.Add(finishStrike, PileType.Hand);
        AutoTestAssert.True(finishStrike.TryManualPlay(enemy2), "Play lethal Strike on Gooped enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(!enemy2.IsAlive, "enemy2 died from lethal Goop strike");
        AutoTestAssert.Equal(hpBefore + 3, player.Creature.CurrentHp, "Black Heart of Goo healed +3 HP on lethal Goop consume");
    }

    internal static async Task ClearHandAndRefillEnergy(
        MegaCrit.Sts2.Core.Entities.Players.Player player,
        MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState pcs)
    {
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        if (pcs.Energy < 10)
            await PlayerCmd.GainEnergy(10 - pcs.Energy, player);
    }
}
