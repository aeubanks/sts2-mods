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

    internal static async Task TestUncommonBatch1AndSpecialistsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSlimeBoss>("SLIME_UNC1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();
        await CreatureCmd.GainMaxHp(enemy1, 1000m);
        await CreatureCmd.GainMaxHp(enemy2, 1000m);

        // 1. All 8 Specialist Slimes verified individually!
        // (a) BronzeSlime: 10 AoE, sets IsStunned=true, skips next attack & unstuns!
        var bronze = AutoTestAssert.NotNull(await SlimeMechanics.SplitSlimeAsync<BronzeSlime>(ctx, player), "Split BronzeSlime");
        int e1Hp = enemy1.CurrentHp;
        int e2Hp = enemy2.CurrentHp;
        await SlimeMechanics.CommandAsync(ctx, player);
        AutoTestAssert.Equal(e1Hp - 10, enemy1.CurrentHp, "BronzeSlime dealt 10 AoE to enemy1");
        AutoTestAssert.Equal(e2Hp - 10, enemy2.CurrentHp, "BronzeSlime dealt 10 AoE to enemy2");
        AutoTestAssert.True(bronze.IsStunned, "BronzeSlime is stunned after attacking");
        e1Hp = enemy1.CurrentHp;
        await SlimeMechanics.CommandAsync(ctx, player);
        AutoTestAssert.Equal(e1Hp, enemy1.CurrentHp, "Stunned BronzeSlime skipped its attack");
        AutoTestAssert.True(!bronze.IsStunned, "BronzeSlime unstunned after skipping");

        // (b) HexSlime: 4 dmg + 6 Soulburn; (c) CultistSlime: 6 dmg then +1 permanent damage!
        await SlimeMechanics.AbsorbAllSlimesAsync(ctx, player);
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        var hex = AutoTestAssert.NotNull(await SlimeMechanics.SplitSlimeAsync<HexSlime>(ctx, player), "Split HexSlime");
        await SlimeMechanics.CommandAsync(ctx, player);
        AutoTestAssert.Equal(6, enemy1.GetPowerAmount<SoulburnPower>() + enemy2.GetPowerAmount<SoulburnPower>(), "HexSlime applied 6 Soulburn");

        var cultist = AutoTestAssert.NotNull(await SlimeMechanics.SplitSlimeAsync<CultistSlime>(ctx, player), "Split CultistSlime");
        AutoTestAssert.Equal(6m, cultist.PassiveVal, "CultistSlime starts at 6 damage");
        await SlimeMechanics.CommandAsync(ctx, player);
        AutoTestAssert.Equal(7m, cultist.PassiveVal, "CultistSlime scaled 6 -> 7 after attacking");

        // (d) TorchHeadSlime: scales with Strength; (e) ProtectorSlime: 4 Temp Thorns; (f) ChampSlime: 1 Vuln; (g) SlowingSlime: 1 Weak; (h) DrawingSlime: 1 draw (no Potency scaling on draw)!
        await SlimeMechanics.AbsorbAllSlimesAsync(ctx, player);
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, 4m, player.Creature, null);
        var torch = AutoTestAssert.NotNull(await SlimeMechanics.SplitSlimeAsync<TorchHeadSlime>(ctx, player), "Split TorchHeadSlime");
        AutoTestAssert.Equal(10m, torch.PassiveVal, "TorchHeadSlime scales with Strength (6 + 4 = 10)");
        int totalHpTorch = enemy1.CurrentHp + enemy2.CurrentHp;
        await SlimeMechanics.CommandAsync(ctx, player);
        AutoTestAssert.Equal(totalHpTorch - 10, enemy1.CurrentHp + enemy2.CurrentHp, "TorchHeadSlime dealt 10 damage with Strength");
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        AutoTestAssert.Equal(6m, torch.PassiveVal, "TorchHeadSlime reverted to 6 damage after Strength removed");

        await SlimeMechanics.SplitSlimeAsync<ProtectorSlime>(ctx, player);
        await SlimeMechanics.CommandAsync(ctx, player);
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<SlimeTempThornsPower>(), "ProtectorSlime granted 4 temporary Thorns");
        await PowerCmd.Remove<SlimeTempThornsPower>(player.Creature);
        await PowerCmd.Remove<ThornsPower>(player.Creature);

        await SlimeMechanics.SplitSlimeAsync<ChampSlime>(ctx, player);
        await SlimeMechanics.CommandAsync(ctx, player);
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<VulnerablePower>() + enemy2.GetPowerAmount<VulnerablePower>(), "ChampSlime applied 1 Vulnerable");

        await SlimeMechanics.SplitSlimeAsync<SlowingSlime>(ctx, player);
        await SlimeMechanics.CommandAsync(ctx, player);
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>() + enemy2.GetPowerAmount<WeakPower>(), "SlowingSlime applied 1 Weak");

        await ClearHandAndRefillEnergy(player, pcs);
        await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        await PowerCmd.Apply<PotencyPower>(ctx, player.Creature, 4m, player.Creature, null);
        var drawing = AutoTestAssert.NotNull(await SlimeMechanics.SplitSlimeAsync<DrawingSlime>(ctx, player), "Split DrawingSlime");
        AutoTestAssert.Equal(7m, drawing.PassiveVal, "DrawingSlime damage scaled with Potency (3 + 4 = 7)");
        AutoTestAssert.Equal(1m, drawing.EvokeVal, "DrawingSlime draw does NOT scale with Potency (stays 1)");
        int handBefore = pcs.Hand.Cards.Count;
        await SlimeMechanics.CommandAsync(ctx, player);
        AutoTestAssert.Equal(handBefore + 1, pcs.Hand.Cards.Count, "DrawingSlime drew 1 card");
        await PowerCmd.Remove<PotencyPower>(player.Creature);
        await PowerCmd.Remove<VulnerablePower>(enemy1);
        await PowerCmd.Remove<VulnerablePower>(enemy2);
        await PowerCmd.Remove<WeakPower>(enemy1);
        await PowerCmd.Remove<WeakPower>(enemy2);
        await PowerCmd.Remove<SoulburnPower>(enemy1);
        await PowerCmd.Remove<SoulburnPower>(enemy2);
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        // 2. AcidTongue (+2 Goop), Gluttony (1 Lick on 1st Consume/turn, IHasSecondAmount), GoopArmor (+3 Block on Consume), LevelUp (+2 Potency)!
        await ClearHandAndRefillEnergy(player, pcs);
        var acid = combatState.CreateCard<AcidTongue>(player);
        var gluttony = combatState.CreateCard<Gluttony>(player);
        var armor = combatState.CreateCard<GoopArmor>(player);
        var lvl = combatState.CreateCard<LevelUp>(player);
        await CardPileCmd.Add(acid, PileType.Hand);
        await CardPileCmd.Add(gluttony, PileType.Hand);
        await CardPileCmd.Add(armor, PileType.Hand);
        await CardPileCmd.Add(lvl, PileType.Hand);
        AutoTestAssert.True(acid.TryManualPlay(null), "Play AcidTongue");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(gluttony.TryManualPlay(null), "Play Gluttony");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(armor.TryManualPlay(null), "Play GoopArmor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(lvl.TryManualPlay(null), "Play LevelUp");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<AcidTonguePower>(), "AcidTonguePower(2)");
        AutoTestAssert.Equal(1, player.Creature.GetPower<GluttonyPower>()!.RemainingThisTurn, "Gluttony starts with 1 charge (no double-add)");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<PotencyPower>(), "LevelUp applied PotencyPower(2)");

        // CorrosiveSpit applies 6 + 2 AcidTongue = 8 Goop!
        var spit = combatState.CreateCard<CorrosiveSpit>(player);
        await CardPileCmd.Add(spit, PileType.Hand);
        AutoTestAssert.True(spit.TryManualPlay(enemy1), "Play CorrosiveSpit with AcidTongue(2)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, enemy1.GetPowerAmount<SlimedPower>(), "AcidTongue boosted CorrosiveSpit 6 -> 8 Goop");

        // GrowthPunch on Gooped enemy1: 4 Block + 3 GoopArmor = 7 Block, triggers Gluttony (+1 Lick, charge -> 0), scales GrowthPunch 4 -> 8!
        var gp = combatState.CreateCard<GrowthPunch>(player);
        await CardPileCmd.Add(gp, PileType.Hand);
        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(gp.TryManualPlay(enemy1), "Play GrowthPunch on Gooped enemy1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 7, player.Creature.Block, "GrowthPunch (4) + GoopArmor (3) = +7 Block");
        AutoTestAssert.Equal(8m, gp.DynamicVars.Block.BaseValue, "GrowthPunch Block scaled 4 -> 8");
        AutoTestAssert.Equal(8m, gp.DynamicVars.Damage.BaseValue, "GrowthPunch Damage scaled 4 -> 8");
        AutoTestAssert.Equal(1, pcs.Hand.Cards.OfType<Lick>().Count(), "Gluttony added 1 Lick on 1st Consume");
        AutoTestAssert.Equal(0, player.Creature.GetPower<GluttonyPower>()!.RemainingThisTurn, "Gluttony charge decremented 1 -> 0");

        // 3. BestDefense (+7 Tackle dmg, +1 Tackle self-dmg) + FlameTackle (18+7=25 dmg, 3+1=4 self-dmg, +5 TackleBuff) + Chomp+ (0-cost Tackle this combat) + ForwardTackle (Command 2)!
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var bestDef = combatState.CreateCard<BestDefense>(player);
        var flame = combatState.CreateCard<FlameTackle>(player);
        var chomp = combatState.CreateCard<Chomp>(player);
        CardCmd.Upgrade(chomp);
        var fwd = combatState.CreateCard<ForwardTackle>(player);
        await CardPileCmd.Add(bestDef, PileType.Hand);
        await CardPileCmd.Add(flame, PileType.Hand);
        await CardPileCmd.Add(chomp, PileType.Hand);
        await CardPileCmd.Add(fwd, PileType.Hand);

        AutoTestAssert.True(bestDef.TryManualPlay(null), "Play BestDefense");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.GetPowerAmount<TackleBuffPower>(), "BestDefense TackleBuffPower(7)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<TackleModifyDamagePower>(), "BestDefense TackleModifyDamagePower(1)");

        int hpBefore = player.Creature.CurrentHp;
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(flame.TryManualPlay(enemy1), "Play FlameTackle (18 + 7 = 25 dmg, 3 + 1 = 4 self-dmg)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 25, enemy1.CurrentHp, "FlameTackle dealt 25 damage with BestDefense");
        AutoTestAssert.Equal(hpBefore - 4, player.Creature.CurrentHp, "FlameTackle dealt 4 self-damage with Reckless");
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<TackleBuffPower>(), "FlameTackle stacked TackleBuffPower 7 -> 12");

        AutoTestAssert.True(chomp.TryManualPlay(enemy1), "Play Chomp+ reducing ForwardTackle to 0 this combat");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, fwd.EnergyCost.GetWithModifiers(CostModifiers.All), "Chomp+ reduced ForwardTackle cost to 0");

        // Play the 0-cost ForwardTackle (18 + 12 TackleBuff = 30 dmg, 3 + 1 Reckless = 4 self-dmg, Command 2x)!
        await SlimeMechanics.SplitSlimeAsync<PoisonSlime>(ctx, player);
        int energyBeforeFwd = pcs.Energy;
        hpBefore = player.Creature.CurrentHp;
        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(fwd.TryManualPlay(enemy1), "Play 0-cost ForwardTackle");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeFwd, pcs.Energy, "0-cost ForwardTackle spent 0 Energy");
        AutoTestAssert.Equal(hpBefore - 4, player.Creature.CurrentHp, "ForwardTackle dealt 4 self-damage with Reckless");
        AutoTestAssert.Equal(e2Hp - 10, enemy2.CurrentHp, "ForwardTackle commanded PoisonSlime (3+2 Potency=5) twice (10 AoE to enemy2)");

        // 4. Equalize (Consume -> triggers twice: 2x8 dmg + 2x4 heal, Exhaust)!
        await ClearHandAndRefillEnergy(player, pcs);
        if (player.Creature.CurrentHp > 35)
            await CreatureCmd.Damage(ctx, player.Creature, 20m, ValueProp.Unblockable | ValueProp.Unpowered, player.Creature);
        await PowerCmd.Remove<AcidTonguePower>(player.Creature);
        await PowerCmd.Remove<GluttonyPower>(player.Creature);
        await PowerCmd.Remove<GoopArmorPower>(player.Creature);
        await PowerCmd.Remove<PotencyPower>(player.Creature);
        await PowerCmd.Remove<TackleBuffPower>(player.Creature);
        await PowerCmd.Remove<TackleModifyDamagePower>(player.Creature);
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await SlimeMechanics.ApplyGoopAsync(ctx, enemy1, player, 4, null);
        var eq = combatState.CreateCard<Equalize>(player);
        await CardPileCmd.Add(eq, PileType.Hand);
        hpBefore = player.Creature.CurrentHp;
        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(eq.TryManualPlay(enemy1), "Play Equalize on Gooped enemy1");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 20, enemy1.CurrentHp, "Equalize dealt (8+4) + 8 = 20 damage across 2 triggers");
        AutoTestAssert.True(player.Creature.CurrentHp >= hpBefore + 8, $"Equalize healed at least 2x4=8 HP (was {hpBefore}, now {player.Creature.CurrentHp})");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(eq), "Equalize exhausted");

        // 5. SlimeboundDissolve (Quick Snack: draw 1, exhaust 2-cost card -> add 2 Licks) + ExhumeLicks priority (non-token HauntingLick before basic Lick!) via DisruptingSlam & HungryTackle!
        await ClearHandAndRefillEnergy(player, pcs);
        await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        var dissolve = combatState.CreateCard<SlimeboundDissolve>(player);
        var twoCost = combatState.CreateCard<LivingWall>(player);
        await CardPileCmd.Add(dissolve, PileType.Hand);
        await CardPileCmd.Add(twoCost, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, twoCost)))
        {
            AutoTestAssert.True(dissolve.TryManualPlay(null), "Play SlimeboundDissolve exhausting 2-cost LivingWall");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(2, pcs.Hand.Cards.OfType<Lick>().Count(), "SlimeboundDissolve added 2 Licks for 2-cost exhausted card");

        // Put 1 HauntingLick (non-token) + 2 basic Licks in ExhaustPile -> HungryTackle (1 exhume) MUST pick HauntingLick first!
        foreach (var ex in pcs.ExhaustPile.Cards.ToList())
            await CardPileCmd.Add(ex, PileType.Discard);
        var hauntEx = combatState.CreateCard<HauntingLick>(player);
        var basicEx = combatState.CreateCard<Lick>(player);
        await CardPileCmd.Add(basicEx, PileType.Exhaust);
        await CardPileCmd.Add(hauntEx, PileType.Exhaust);
        await ClearHandAndRefillEnergy(player, pcs);
        var hungry = combatState.CreateCard<HungryTackle>(player);
        await CardPileCmd.Add(hungry, PileType.Hand);
        AutoTestAssert.True(hungry.TryManualPlay(enemy1), "Play HungryTackle exhuming non-token Lick first");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.Hand.Cards.Contains(hauntEx), "ExhumeLicks preferred non-token HauntingLick over basic Lick");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(basicEx), "Basic Lick remained in Exhaust Pile");

        var slam = combatState.CreateCard<DisruptingSlam>(player);
        await CardPileCmd.Add(slam, PileType.Hand);
        AutoTestAssert.True(slam.TryManualPlay(null), "Play DisruptingSlam exhuming remaining basic Lick");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.Hand.Cards.Contains(basicEx), "DisruptingSlam exhumed remaining basic Lick");

        // 6. LeadByExample (1st enemy-targeting card commands leading Slime) + DivideAndConquer (10 dmg/slime + Absorb all -> +Strength) + Grow (lose 1 slot -> +2 Str, +2 Dex)!
        await ClearHandAndRefillEnergy(player, pcs);
        await SlimeMechanics.AbsorbAllSlimesAsync(ctx, player);
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await SlimeMechanics.SplitSlimeAsync<PoisonSlime>(ctx, player);
        await SlimeMechanics.SplitSlimeAsync<PoisonSlime>(ctx, player);
        var lead = combatState.CreateCard<LeadByExample>(player);
        var strike = combatState.CreateCard<SlimeboundStrike>(player);
        var dnc = combatState.CreateCard<DivideAndConquer>(player);
        var grow = combatState.CreateCard<Grow>(player);
        await CardPileCmd.Add(lead, PileType.Hand);
        await CardPileCmd.Add(strike, PileType.Hand);
        await CardPileCmd.Add(dnc, PileType.Hand);
        await CardPileCmd.Add(grow, PileType.Hand);

        AutoTestAssert.True(lead.TryManualPlay(null), "Play LeadByExample");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPower<ComboAttackPower>()!.RemainingThisTurn, "LeadByExample starts with 1 charge");

        // Non-enemy-targeting Defend must NOT consume LeadByExample charge!
        var defNonTarget = combatState.CreateCard<SlimeboundDefend>(player);
        await CardPileCmd.Add(defNonTarget, PileType.Hand);
        AutoTestAssert.True(defNonTarget.TryManualPlay(null), "Play Self-targeting Defend");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPower<ComboAttackPower>()!.RemainingThisTurn, "Defend did NOT consume LeadByExample charge");

        e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(strike.TryManualPlay(enemy1), "Play Strike triggering LeadByExample Command (3 AoE from PoisonSlime)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 3, enemy2.CurrentHp, "LeadByExample commanded leading PoisonSlime (3 AoE to enemy2)");
        AutoTestAssert.Equal(0, player.Creature.GetPower<ComboAttackPower>()!.RemainingThisTurn, "LeadByExample charge decremented to 0");

        int totalHp = enemy1.CurrentHp + enemy2.CurrentHp;
        AutoTestAssert.True(dnc.TryManualPlay(null), "Play DivideAndConquer with 2 Slimes (2x10=20 dmg + Absorb 2 -> +2 Strength)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(totalHp - 20, enemy1.CurrentHp + enemy2.CurrentHp, "DivideAndConquer dealt 20 total damage for 2 Slimes");
        AutoTestAssert.Equal(0, SlimeMechanics.GetSlimes(player).Count, "DivideAndConquer absorbed all Slimes");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Absorbing 2 Slimes granted +2 Strength");

        int slotsBefore = pcs.OrbQueue.Capacity;
        AutoTestAssert.True(grow.TryManualPlay(null), "Play Grow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(slotsBefore - 1, pcs.OrbQueue.Capacity, "Grow reduced Slime slot capacity by 1");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<StrengthPower>(), "Grow granted +2 Strength (2+2=4)");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DexterityPower>(), "Grow granted +2 Dexterity");

        // Verify SpawnSpecialistSlimeAsync + turn-start reset of ComboAttackPower!
        var spec = AutoTestAssert.NotNull(await SlimeMechanics.SpawnSpecialistSlimeAsync(ctx, player), "SpawnSpecialistSlimeAsync spawned a Specialist Slime");
        AutoTestAssert.True(pcs.OrbQueue.Orbs.Contains(spec), "Spawned Specialist Slime is in OrbQueue");
        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(1, player.Creature.GetPower<ComboAttackPower>()!.RemainingThisTurn, "ComboAttackPower charge reset to 1 at start of Round 2");
    }

    internal static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheSlimeBoss>("SLIME_UNC2");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy1 = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy 1");
        var enemy2 = await CreatureCmd.Add<MegaCrit.Sts2.Core.Models.Monsters.Nibbit>(combatState);
        var ctx = new ThrowingPlayerChoiceContext();
        await CreatureCmd.GainMaxHp(enemy1, 1000m);
        await CreatureCmd.GainMaxHp(enemy2, 1000m);

        // 1. MegaLick+, SamplingLick+, Nibble+ (all IsLick + Exhaust + draw 1 when upgraded), then TongueLash (6 + 2*3 = 12) & Recollect (exhumes 1 Lick)!
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);

        var mega = combatState.CreateCard<MegaLick>(player);
        var sampling = combatState.CreateCard<SamplingLick>(player);
        var nibble = combatState.CreateCard<Nibble>(player);
        CardCmd.Upgrade(mega);
        CardCmd.Upgrade(sampling);
        CardCmd.Upgrade(nibble);
        await CardPileCmd.Add(mega, PileType.Hand);
        await CardPileCmd.Add(sampling, PileType.Hand);
        await CardPileCmd.Add(nibble, PileType.Hand);

        AutoTestAssert.True(mega.TryManualPlay(null), "Play MegaLick+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy1.GetPowerAmount<WeakPower>(), "MegaLick+ applied 1 Weak to enemy1");
        AutoTestAssert.Equal(1, enemy2.GetPowerAmount<WeakPower>(), "MegaLick+ applied 1 Weak to enemy2");
        AutoTestAssert.Equal(4, enemy1.GetPowerAmount<SlimedPower>(), "MegaLick+ applied 4 Goop to enemy1");
        AutoTestAssert.Equal(4, enemy2.GetPowerAmount<SlimedPower>(), "MegaLick+ applied 4 Goop to enemy2");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(mega), "MegaLick+ exhausted");

        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(sampling.TryManualPlay(enemy1), "Play SamplingLick+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, enemy1.GetPowerAmount<SlimedPower>(), "SamplingLick+ stacked Goop 4 -> 8");
        AutoTestAssert.Equal(blockBefore + 4, player.Creature.Block, "SamplingLick+ granted 4 Block");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(sampling), "SamplingLick+ exhausted");

        await PowerCmd.Remove<SlimedPower>(enemy1);
        await PowerCmd.Remove<SlimedPower>(enemy2);
        AutoTestAssert.True(nibble.TryManualPlay(enemy1), "Play Nibble+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.Hand.Cards.OfType<Lick>().Any(), "Nibble+ added Lick to hand");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(nibble), "Nibble+ exhausted");

        // 3 Exhausted Licks -> TongueLash deals 6 + 3*2 = 12; TongueLash+ deals 6 + 3*3 = 15!
        await ClearHandAndRefillEnergy(player, pcs);
        var lash = combatState.CreateCard<TongueLash>(player);
        var lashPlus = combatState.CreateCard<TongueLash>(player);
        CardCmd.Upgrade(lashPlus);
        await CardPileCmd.Add(lash, PileType.Hand);
        await CardPileCmd.Add(lashPlus, PileType.Hand);

        int e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(lash.TryManualPlay(enemy1), "Play TongueLash with 3 Exhausted Licks (6 + 6 = 12)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 12, enemy1.CurrentHp, "TongueLash dealt 12 damage");

        e1Hp = enemy1.CurrentHp;
        AutoTestAssert.True(lashPlus.TryManualPlay(enemy1), "Play TongueLash+ with 3 Exhausted Licks (6 + 9 = 15)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 15, enemy1.CurrentHp, "TongueLash+ dealt 15 damage");

        // Recollect (8 Block + exhumes 1 Lick from Exhaust Pile)
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var recollect = combatState.CreateCard<Recollect>(player);
        await CardPileCmd.Add(recollect, PileType.Hand);
        int exBefore = pcs.ExhaustPile.Cards.Count;
        AutoTestAssert.True(recollect.TryManualPlay(null), "Play Recollect");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.Block, "Recollect granted 8 Block");
        AutoTestAssert.Equal(exBefore - 1, pcs.ExhaustPile.Cards.Count, "Recollect exhumed 1 Lick from Exhaust Pile");

        // 2. RainOfGoop (4 Goop x 3 random hits = 12 total) + ViciousTackle (17 + 2x Goop + 3 self-damage)!
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Remove<SlimedPower>(enemy1);
        await PowerCmd.Remove<SlimedPower>(enemy2);
        var rain = combatState.CreateCard<RainOfGoop>(player);
        await CardPileCmd.Add(rain, PileType.Hand);
        AutoTestAssert.True(rain.TryManualPlay(null), "Play RainOfGoop");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, enemy1.GetPowerAmount<SlimedPower>() + enemy2.GetPowerAmount<SlimedPower>(), "RainOfGoop applied 12 total Goop");

        await PowerCmd.Remove<SlimedPower>(enemy1);
        await SlimeMechanics.ApplyGoopAsync(ctx, enemy1, player, 5, null);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var vicious = combatState.CreateCard<ViciousTackle>(player);
        await CardPileCmd.Add(vicious, PileType.Hand);
        e1Hp = enemy1.CurrentHp;
        int hpBefore = player.Creature.CurrentHp;
        AutoTestAssert.True(vicious.TryManualPlay(enemy1), "Play ViciousTackle on 5-Goop enemy (17 + 10 = 27 dmg, 3 self-dmg)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e1Hp - 27, enemy1.CurrentHp, "ViciousTackle gained 2x Goop bonus (17 + 10 = 27)");
        AutoTestAssert.Equal(hpBefore - 3, player.Creature.CurrentHp, "ViciousTackle dealt 3 self-damage");
        AutoTestAssert.Equal(0, enemy1.GetPowerAmount<SlimedPower>(), "Goop was consumed by ViciousTackle");

        // 3. ProtectTheBoss -> SlimeSacrificePower: prevents enemy attack HP loss by absorbing leading Slime (+1 Strength), does NOT prevent if 0 Slimes!
        await ClearHandAndRefillEnergy(player, pcs);
        await SlimeMechanics.AbsorbAllSlimesAsync(ctx, player);
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<WeakPower>(enemy1);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);

        var protect = combatState.CreateCard<ProtectTheBoss>(player);
        await CardPileCmd.Add(protect, PileType.Hand);
        AutoTestAssert.True(protect.TryManualPlay(null), "Play ProtectTheBoss");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<SlimeSacrificePower>(), "ProtectTheBoss applied SlimeSacrificePower(1)");

        // With 0 Slimes: enemy attack still damages player and power remains!
        hpBefore = player.Creature.CurrentHp;
        await CreatureCmd.Damage(ctx, player.Creature, 4m, ValueProp.Move, enemy1);
        AutoTestAssert.Equal(hpBefore - 4, player.Creature.CurrentHp, "SlimeSacrificePower did NOT trigger with 0 Slimes");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<SlimeSacrificePower>(), "SlimeSacrificePower stack preserved when 0 Slimes");

        // With 2 Slimes: blocked attack & non-attack damage do NOT trigger SlimeSacrificePower;
        // a 2-hit enemy attack prevents hit 1 (absorbing 1 Slime, +1 Strength, decrementing power to 0) and hit 2 deals damage!
        await SlimeMechanics.SplitSlimeAsync<ShieldSlime>(ctx, player);
        await SlimeMechanics.SplitSlimeAsync<ShieldSlime>(ctx, player);
        await CreatureCmd.GainBlock(player.Creature, 10m, ValueProp.Unpowered, null);
        hpBefore = player.Creature.CurrentHp;
        await CreatureCmd.Damage(ctx, player.Creature, 6m, ValueProp.Move, enemy1);
        AutoTestAssert.Equal(hpBefore, player.Creature.CurrentHp, "Blocked enemy attack dealt 0 HP loss");
        AutoTestAssert.Equal(2, SlimeMechanics.GetSlimes(player).Count, "Blocked enemy attack did NOT trigger SlimeSacrificePower");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<SlimeSacrificePower>(), "SlimeSacrificePower preserved on blocked attack");
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);

        hpBefore = player.Creature.CurrentHp;
        await CreatureCmd.Damage(ctx, player.Creature, 3m, ValueProp.Unpowered, enemy1);
        AutoTestAssert.Equal(hpBefore - 3, player.Creature.CurrentHp, "Non-attack damage bypassed SlimeSacrificePower");
        AutoTestAssert.Equal(2, SlimeMechanics.GetSlimes(player).Count, "Non-attack damage did NOT absorb a Slime");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<SlimeSacrificePower>(), "SlimeSacrificePower preserved on non-attack damage");

        hpBefore = player.Creature.CurrentHp;
        await DamageCmd.Attack(5m).FromMonster(enemy1.Monster!).WithHitCount(2).Execute(ctx);
        AutoTestAssert.Equal(hpBefore - 5, player.Creature.CurrentHp, "Multi-hit enemy attack: 1st hit prevented by SlimeSacrificePower, 2nd hit dealt 5 damage");
        AutoTestAssert.Equal(1, SlimeMechanics.GetSlimes(player).Count, "SlimeSacrificePower absorbed only 1 leading Slime on 2-hit attack");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "Absorbed Slime granted +1 Strength");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<SlimeSacrificePower>(), "SlimeSacrificePower decremented to 0");
        await SlimeMechanics.AbsorbAllSlimesAsync(ctx, player);

        // 4. SlimeTap (unplayable with 0 Slimes; playable with Slime -> Absorb + Draw 2 + Gain 2 Energy when upgraded, Exhaust)!
        await ClearHandAndRefillEnergy(player, pcs);
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        var tap = combatState.CreateCard<SlimeTap>(player);
        CardCmd.Upgrade(tap);
        await CardPileCmd.Add(tap, PileType.Hand);
        AutoTestAssert.True(!tap.CanPlay(), "SlimeTap is unplayable with 0 Slimes");

        await SlimeMechanics.SplitSlimeAsync<ShieldSlime>(ctx, player);
        AutoTestAssert.True(tap.CanPlay(), "SlimeTap is playable with a Slime");
        int energyBefore = pcs.Energy;
        int handBefore = pcs.Hand.Cards.Count;
        AutoTestAssert.True(tap.TryManualPlay(null), "Play SlimeTap+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, SlimeMechanics.GetSlimes(player).Count, "SlimeTap+ absorbed leading Slime");
        AutoTestAssert.Equal(energyBefore + 2, pcs.Energy, "SlimeTap+ gained 2 Energy");
        AutoTestAssert.Equal(handBefore - 1 + 2, pcs.Hand.Cards.Count, "SlimeTap+ drew 2 cards");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(tap), "SlimeTap+ exhausted");

        // 5. Repurpose+ (Absorb leading Slime -> Split random Specialist -> Command) + SplitSpecialist+ (1-of-3 Specialist choice + Command)!
        await ClearHandAndRefillEnergy(player, pcs);
        await SlimeMechanics.SplitSlimeAsync<ShieldSlime>(ctx, player);
        var repurpose = combatState.CreateCard<Repurpose>(player);
        CardCmd.Upgrade(repurpose);
        await CardPileCmd.Add(repurpose, PileType.Hand);
        AutoTestAssert.True(repurpose.TryManualPlay(null), "Play Repurpose+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, SlimeMechanics.GetSlimes(player).Count, "Repurpose+ replaced Slime with 1 Specialist Slime");
        AutoTestAssert.True(SlimeMechanics.GetLeadingSlime(player) is not ShieldSlime, "Repurpose+ spawned a Specialist Slime");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(repurpose), "Repurpose+ exhausted");

        var splitSpec = combatState.CreateCard<SplitSpecialist>(player);
        CardCmd.Upgrade(splitSpec);
        await CardPileCmd.Add(splitSpec, PileType.Hand);
        int slimeCountBefore = SlimeMechanics.GetSlimes(player).Count;
        AutoTestAssert.True(splitSpec.TryManualPlay(null), "Play SplitSpecialist+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(slimeCountBefore + 1, SlimeMechanics.GetSlimes(player).Count, "SplitSpecialist+ spawned 1 chosen Specialist Slime");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(splitSpec), "SplitSpecialist+ exhausted");

        // 6. SlimeBarrage+ (8 dmg + Command ALL Slimes TWICE!) + ServeAndProtect (Block*slimes + Blur*slimes + Absorb all)!
        await ClearHandAndRefillEnergy(player, pcs);
        await SlimeMechanics.AbsorbAllSlimesAsync(ctx, player);
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<WeakPower>(enemy1);
        await PowerCmd.Remove<WeakPower>(enemy2);
        await PowerCmd.Remove<VulnerablePower>(enemy1);
        await PowerCmd.Remove<VulnerablePower>(enemy2);
        await PowerCmd.Remove<SlimedPower>(enemy1);
        await PowerCmd.Remove<SlimedPower>(enemy2);
        await SlimeMechanics.SplitSlimeAsync<PoisonSlime>(ctx, player);
        await SlimeMechanics.SplitSlimeAsync<PoisonSlime>(ctx, player);

        var barrage = combatState.CreateCard<SlimeBarrage>(player);
        CardCmd.Upgrade(barrage);
        await CardPileCmd.Add(barrage, PileType.Hand);
        int e2Hp = enemy2.CurrentHp;
        AutoTestAssert.True(barrage.TryManualPlay(enemy1), "Play SlimeBarrage+ with 2 PoisonSlimes (2*3*2 = 12 AoE to enemy2)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(e2Hp - 12, enemy2.CurrentHp, "SlimeBarrage+ commanded ALL 2 Slimes twice (12 AoE to enemy2)");

        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var serve = combatState.CreateCard<ServeAndProtect>(player);
        await CardPileCmd.Add(serve, PileType.Hand);
        AutoTestAssert.True(serve.TryManualPlay(null), "Play ServeAndProtect with 2 Slimes");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(20, player.Creature.Block, "ServeAndProtect granted 2x10 = 20 Block");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<BlurPower>(), "ServeAndProtect granted 2 Blur");
        AutoTestAssert.Equal(0, SlimeMechanics.GetSlimes(player).Count, "ServeAndProtect absorbed all Slimes");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Absorbing 2 Slimes granted +2 Strength");

        // 7. Replication (clone chosen hand card onto top of Draw Pile) + SlimeBrawl (auto-play 1 + slimes top cards)!
        await ClearHandAndRefillEnergy(player, pcs);
        var targetCard = combatState.CreateCard<SlimeboundDefend>(player);
        var replication = combatState.CreateCard<Replication>(player);
        await CardPileCmd.Add(targetCard, PileType.Hand);
        await CardPileCmd.Add(replication, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, targetCard)))
        {
            AutoTestAssert.True(replication.TryManualPlay(null), "Play Replication cloning SlimeboundDefend onto top of Draw Pile");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        var topDraw = pcs.DrawPile.Cards.FirstOrDefault();
        AutoTestAssert.True(topDraw is SlimeboundDefend, "Replication placed clone on top of Draw Pile");
        AutoTestAssert.True(!ReferenceEquals(topDraw, targetCard), "Replication placed a distinct clone rather than moving original");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(targetCard), "Replication kept original card in Hand");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(replication), "Replication exhausted");

        // Put a second SlimeboundDefend on top of Draw Pile, split 1 Slime, play SlimeBrawl -> auto-plays 2 Defends (+10 Block)!
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        await SlimeMechanics.SplitSlimeAsync<ShieldSlime>(ctx, player);
        var brawl = combatState.CreateCard<SlimeBrawl>(player);
        await CardPileCmd.Add(brawl, PileType.Hand);
        AutoTestAssert.True(brawl.TryManualPlay(null), "Play SlimeBrawl with 1 Slime (plays top 2 Defends -> +10 Block)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(10, player.Creature.Block, "SlimeBrawl auto-played 1 + 1 = 2 Defends (+10 Block)");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(brawl), "SlimeBrawl exhausted");

        // 8. Prepare (10 Block + EnergyNextTurn(1) + DrawCardsNextTurn(2), Exhaust) -> verify Round 2 gets +1 Energy & +2 Draw!
        await ClearHandAndRefillEnergy(player, pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        for (int i = 0; i < 8; i++)
            await CardPileCmd.Add(combatState.CreateCard<SlimeboundDefend>(player), PileType.Draw, CardPilePosition.Top);
        var prepare = combatState.CreateCard<Prepare>(player);
        await CardPileCmd.Add(prepare, PileType.Hand);
        AutoTestAssert.True(prepare.TryManualPlay(null), "Play Prepare");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(10, player.Creature.Block, "Prepare granted 10 Block");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnergyNextTurnPower>(), "Prepare applied EnergyNextTurnPower(1)");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "Prepare applied DrawCardsNextTurnPower(2)");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(prepare), "Prepare exhausted");

        await CreatureCmd.GainBlock(player.Creature, 80m, ValueProp.Unpowered, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(4, pcs.Energy, "Round 2 started with 3 + 1 = 4 Energy from Prepare");
        AutoTestAssert.Equal(7, pcs.Hand.Cards.Count, "Round 2 started with 5 + 2 = 7 cards from Prepare");
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
