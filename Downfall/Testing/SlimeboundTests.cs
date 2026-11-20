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

    internal static async Task TestCommonCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSlimeBoss>("SLIME_COM01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();
        await CreatureCmd.GainMaxHp(enemy1, 800m);
        await CreatureCmd.GainMaxHp(enemy2, 800m);

        // 1. GoopSpray (5 Goop + 1 Weak ALL) + RejuvenatingLick+ (4 Goop x2 + draw 1, Exhaust) + HauntingLick+ (1 Vuln + 4 Goop + draw 1, Exhaust)!
        await ClearHandAndRefillEnergy(player, pcs);
        await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        var spray = combatState.CreateCard<GoopSpray>(player);
        var dblLick = combatState.CreateCard<RejuvenatingLick>(player);
        var haunt = combatState.CreateCard<HauntingLick>(player);
        CardCmd.Upgrade(dblLick);
        CardCmd.Upgrade(haunt);
        await CardPileCmd.Add(spray, PileType.Hand);
        await CardPileCmd.Add(dblLick, PileType.Hand);
        await CardPileCmd.Add(haunt, PileType.Hand);

        AutoTestAssert.True(spray.TryManualPlay(null), "Play GoopSpray");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, enemy1.GetPowerAmount<SlimedPower>(), "GoopSpray applied 5 Goop to enemy1");
        AutoTestAssert.Equal(5, enemy2.GetPowerAmount<SlimedPower>(), "GoopSpray applied 5 Goop to enemy2");
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>(), "GoopSpray applied 1 Weak to enemy1");
        AutoTestAssert.Equal(1, enemy2.GetPowerAmount<WeakPower>(), "GoopSpray applied 1 Weak to enemy2");

        int handBefore = pcs.Hand.Cards.Count;
        AutoTestAssert.True(dblLick.TryManualPlay(enemy1), "Play RejuvenatingLick+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(13, enemy1.GetPowerAmount<SlimedPower>(), "RejuvenatingLick+ applied 4+4=8 Goop (5+8=13)");
        AutoTestAssert.Equal(handBefore, pcs.Hand.Cards.Count, "RejuvenatingLick+ drew 1 card");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(dblLick), "RejuvenatingLick+ exhausted");

        handBefore = pcs.Hand.Cards.Count;
        AutoTestAssert.True(haunt.TryManualPlay(enemy2), "Play HauntingLick+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(9, enemy2.GetPowerAmount<SlimedPower>(), "HauntingLick+ applied 4 Goop (5+4=9)");
        AutoTestAssert.Equal(1, enemy2.GetPowerAmount<VulnerablePower>(), "HauntingLick+ applied 1 Vulnerable");
        AutoTestAssert.Equal(handBefore, pcs.Hand.Cards.Count, "HauntingLick+ drew 1 card");
        await PowerCmd.Remove<WeakPower>(enemy1);
        await PowerCmd.Remove<WeakPower>(enemy2);
        await PowerCmd.Remove<VulnerablePower>(enemy2);

        // 2. Consume cards: LeechingTouch (Block = Goop=13, CardTag.Strike, UsesConsumeGlow), ItLooksTasty+ (Lick+), LeechEnergy (+1 Energy + 1 draw)!
        await ClearHandAndRefillEnergy(player, pcs);
        var leechStrike = combatState.CreateCard<LeechingTouch>(player);
        await CardPileCmd.Add(leechStrike, PileType.Hand);
        AutoTestAssert.True(leechStrike.Tags.Contains(CardTag.Strike), "LeechingTouch has CardTag.Strike");
        AutoTestAssert.True(leechStrike.ShouldGlowGold, "LeechingTouch glows gold when enemy has Goop");
        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(leechStrike.TryManualPlay(enemy1), "Play LeechingTouch on 13-Goop enemy1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 13, player.Creature.Block, "LeechingTouch gained Block equal to snapshotted Goop (13)");
        AutoTestAssert.Equal(0, enemy1.GetPowerAmount<SlimedPower>(), "LeechingTouch consumed Goop");

        // Negative Consume check: playing LeechingTouch on Goop-less enemy1 grants 0 Block!
        var leechNoGoop = combatState.CreateCard<LeechingTouch>(player);
        await CardPileCmd.Add(leechNoGoop, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(leechNoGoop.TryManualPlay(enemy1), "Play LeechingTouch on Goop-less enemy1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore, player.Creature.Block, "LeechingTouch did NOT gain Block without Goop");

        var tasty = combatState.CreateCard<ItLooksTasty>(player);
        CardCmd.Upgrade(tasty);
        await CardPileCmd.Add(tasty, PileType.Hand);
        AutoTestAssert.True(tasty.TryManualPlay(enemy2), "Play ItLooksTasty+ on Gooped enemy2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        var addedLick = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<Lick>().FirstOrDefault(), "ItLooksTasty+ added Lick to Hand");
        AutoTestAssert.True(addedLick.IsUpgraded, "ItLooksTasty+ added Upgraded Lick+");

        // Use Schlurp+ (7 Goop + 2 Licks) then LeechEnergy (Consume -> +1 Energy + 1 draw)!
        var schlurp = combatState.CreateCard<Schlurp>(player);
        CardCmd.Upgrade(schlurp);
        await CardPileCmd.Add(schlurp, PileType.Hand);
        AutoTestAssert.True(schlurp.TryManualPlay(enemy1), "Play Schlurp+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, enemy1.GetPowerAmount<SlimedPower>(), "Schlurp+ applied 7 Goop");
        AutoTestAssert.Equal(3, pcs.Hand.Cards.OfType<Lick>().Count(), "Schlurp+ added 2 more Licks (1+2=3)");

        await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        var leechEnergy = combatState.CreateCard<LeechEnergy>(player);
        await CardPileCmd.Add(leechEnergy, PileType.Hand);
        int energyBefore = pcs.Energy;
        handBefore = pcs.Hand.Cards.Count;
        AutoTestAssert.True(leechEnergy.TryManualPlay(enemy1), "Play LeechEnergy on Gooped enemy1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBefore, pcs.Energy, "LeechEnergy spent 1 and gained +1 Energy");
        AutoTestAssert.Equal(handBefore, pcs.Hand.Cards.Count, "LeechEnergy drew 1 card");

        // 3. RollThrough (6 AoE + PreventTackleDamagePower 2) + FinishingTackle (0 self-dmg via Prevent, Consume -> 2 Vuln) + ComboTackle (0 self-dmg via Prevent, draw 2) + GoopTackle (3 self-dmg after charges spent, adds 0-cost random Tackle)!
        await ClearHandAndRefillEnergy(player, pcs);
        for (int i = 0; i < 3; i++)
            await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        var roll = combatState.CreateCard<RollThrough>(player);
        var finish = combatState.CreateCard<FinishingTackle>(player);
        var combo = combatState.CreateCard<ComboTackle>(player);
        var goopTackle = combatState.CreateCard<GoopTackle>(player);
        await CardPileCmd.Add(roll, PileType.Hand);
        await CardPileCmd.Add(finish, PileType.Hand);
        await CardPileCmd.Add(combo, PileType.Hand);
        await CardPileCmd.Add(goopTackle, PileType.Hand);

        AutoTestAssert.True(roll.TryManualPlay(null), "Play RollThrough");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<PreventTackleDamagePower>(), "RollThrough applied PreventTackleDamagePower(2)");

        // Apply Goop after RollThrough so FinishingTackle consumes it!
        await SlimeMechanics.ApplyGoopAsync(ctx, enemy1, player, 4, null);

        int hpBefore = player.Creature.CurrentHp;
        AutoTestAssert.True(finish.TryManualPlay(enemy1), "Play FinishingTackle (prevent charge #1)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore, player.Creature.CurrentHp, "RollThrough prevented FinishingTackle self-damage");
        AutoTestAssert.Equal(2, enemy1.GetPowerAmount<VulnerablePower>(), "FinishingTackle Consume applied 2 Vulnerable");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<PreventTackleDamagePower>(), "PreventTackleDamagePower decremented 2 -> 1");

        handBefore = pcs.Hand.Cards.Count;
        AutoTestAssert.True(combo.TryManualPlay(enemy1), "Play ComboTackle (prevent charge #2)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore, player.Creature.CurrentHp, "RollThrough prevented ComboTackle self-damage");
        AutoTestAssert.Equal(handBefore - 1 + 2, pcs.Hand.Cards.Count, "ComboTackle drew 2 cards");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<PreventTackleDamagePower>(), "PreventTackleDamagePower consumed");

        // Now GoopTackle takes 3 self-damage (unblocked -> drop block first) and adds a 0-cost other Tackle!
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        hpBefore = player.Creature.CurrentHp;
        AutoTestAssert.True(goopTackle.TryManualPlay(enemy1), "Play GoopTackle");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 3, player.Creature.CurrentHp, "GoopTackle dealt 3 self-damage once Prevent expired");
        var genTackle = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<SlimeCard>().FirstOrDefault(c => c.IsTackle && c is not GoopTackle),
            "GoopTackle generated a random other Tackle card");
        AutoTestAssert.Equal(0, genTackle.EnergyCost.GetWithModifiers(CostModifiers.All), "Generated Tackle costs 0 this turn");

        // 4. Split cards (SplitBruiser, SplitLeeching, SplitLicking, SplitAcid) + PressTheAttack+ (Command twice)!
        await ClearHandAndRefillEnergy(player, pcs);
        await SlimeMechanics.AbsorbAllSlimesAsync(ctx, player);
        var sBruiser = combatState.CreateCard<SplitBruiser>(player);
        var sLeeching = combatState.CreateCard<SplitLeeching>(player);
        var sLicking = combatState.CreateCard<SplitLicking>(player);
        var sAcid = combatState.CreateCard<SplitAcid>(player);
        await CardPileCmd.Add(sBruiser, PileType.Hand);
        await CardPileCmd.Add(sLeeching, PileType.Hand);
        await CardPileCmd.Add(sLicking, PileType.Hand);
        await CardPileCmd.Add(sAcid, PileType.Hand);

        int totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(sBruiser.TryManualPlay(null), "Play SplitBruiser (2 commands x 2 hits x 3 dmg = 12)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp - 12, enemy1.CurrentHp + enemy2.CurrentHp, "SplitBruiser dealt 12 total damage");

        blockBefore = player.Creature.Block;
        AutoTestAssert.True(sLeeching.TryManualPlay(null), "Play SplitLeeching (2 commands x 3 Block = 6)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 6, player.Creature.Block, "SplitLeeching granted +6 Block");

        AutoTestAssert.True(sLicking.TryManualPlay(null), "Play SplitLicking (2 commands x 2 Goop = 4)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, enemy1.GetPowerAmount<SlimedPower>() + enemy2.GetPowerAmount<SlimedPower>(), "SplitLicking applied 4 total Goop");

        int e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(sAcid.TryManualPlay(null), "Play SplitAcid (2 commands x 3 AoE = 6 each, auto-evokes oldest Bruiser)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 6, enemy1.CurrentHp, "SplitAcid dealt 6 AoE to enemy1");
        AutoTestAssert.Equal(e2Hp - 6, enemy2.CurrentHp, "SplitAcid dealt 6 AoE to enemy2");

        var press = combatState.CreateCard<PressTheAttack>(player);
        CardCmd.Upgrade(press);
        await CardPileCmd.Add(press, PileType.Hand);
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(press.TryManualPlay(enemy1), "Play PressTheAttack+ (Commands leading PoisonSlime twice = 6 AoE)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 6, enemy2.CurrentHp, "PressTheAttack+ commanded leading PoisonSlime twice (6 dmg to enemy2)");

        // 5. SelfFormingGoo (8 Block, Discard -> top of Draw) + SlimeSpikes stacking (2x + permanent Thorns preservation!) + LivingWall (SlimedThornsPower applies 4 Goop to attacker!)
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Remove<SlimedPower>(enemy1);
        await PowerCmd.Apply<ThornsPower>(ctx, player.Creature, 2m, player.Creature, null); // 2 permanent Thorns
        var targetDiscard = combatState.CreateCard<Tackle>(player);
        await CardPileCmd.Add(targetDiscard, PileType.Discard);
        var reform = combatState.CreateCard<SelfFormingGoo>(player);
        var spikes1 = combatState.CreateCard<SlimeSpikes>(player);
        var spikes2 = combatState.CreateCard<SlimeSpikes>(player);
        var wall = combatState.CreateCard<LivingWall>(player);
        await CardPileCmd.Add(reform, PileType.Hand);
        await CardPileCmd.Add(spikes1, PileType.Hand);
        await CardPileCmd.Add(spikes2, PileType.Hand);
        await CardPileCmd.Add(wall, PileType.Hand);

        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, targetDiscard)))
        {
            AutoTestAssert.True(reform.TryManualPlay(null), "Play SelfFormingGoo");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(targetDiscard, pcs.DrawPile.Cards.FirstOrDefault(), "SelfFormingGoo put Tackle on top of Draw Pile");

        AutoTestAssert.True(spikes1.TryManualPlay(null), "Play SlimeSpikes #1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, player.Creature.GetPowerAmount<ThornsPower>(), "SlimeSpikes #1 stacked Thorns 2 -> 5");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<SlimeTempThornsPower>(), "SlimeSpikes #1 applied SlimeTempThornsPower(3)");

        AutoTestAssert.True(spikes2.TryManualPlay(null), "Play SlimeSpikes #2 (stacking check)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.GetPowerAmount<ThornsPower>(), "SlimeSpikes #2 stacked Thorns 5 -> 8 (no double-apply)");
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<SlimeTempThornsPower>(), "SlimeSpikes #2 stacked SlimeTempThornsPower 3 -> 6");

        AutoTestAssert.True(wall.TryManualPlay(null), "Play LivingWall");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<SlimedThornsPower>(), "LivingWall applied SlimedThornsPower(4)");

        // Simulate powered enemy attack from enemy1 -> verifies SlimedThornsPower applies 4 Goop AND Thorns(8) damages enemy1!
        e1Hp = enemy1.CurrentHp;
        await CreatureCmd.Damage(ctx, player.Creature, 5m, ValueProp.Move, enemy1);
        AutoTestAssert.Equal(4, enemy1.GetPowerAmount<SlimedPower>(), "SlimedThornsPower applied 4 Goop to attacking enemy1");
        AutoTestAssert.Equal(e1Hp - 8, enemy1.CurrentHp, "Stacked Thorns(8) dealt 8 damage to attacking enemy1");

        // End Turn 1 -> temporary Thorns (6) removed leaving 2 permanent Thorns, and SlimedThornsPower expires by Round 2!
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<ThornsPower>(), "Temporary Thorns expired leaving 2 permanent Thorns at Round 2");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<SlimeTempThornsPower>(), "SlimeTempThornsPower removed");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<SlimedThornsPower>(), "SlimedThornsPower expired at start of Round 2");
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
