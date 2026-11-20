using Downfall.Core;
using Downfall.Hexaghost;
using Downfall.Hexaghost.Cards;
using Downfall.Hexaghost.Orbs;
using Downfall.Hexaghost.Powers;
using Downfall.Hexaghost.Relics;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Orbs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Downfall.Testing;

internal static class HexaghostTests
{
    private static async Task PlayCardAsync(CardModel card, Creature? target = null)
    {
        AutoTestAssert.True(card.TryManualPlay(target), $"TryManualPlay({card.GetType().Name})");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
    }
    internal static async Task TestVisualsRuntime()
    {
        await DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheHexaghost>(
            "HEXA_VIS",
            path => path == "res://Downfall/images/hexaghost/char/hexaghost.png");
        DownfallAutoTestRunner.CleanupRun();

        var (player, _, _) = await DownfallAutoTestRunner.StartCombatAsync<TheHexaghost>("HEXA_ORBVIS");
        var ctx = new ThrowingPlayerChoiceContext();

        var orbManager = AutoTestAssert.NotNull(
            NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.OrbManager,
            "Hexaghost live NOrbManager");

        var flames = HexaMechanics.GetGhostflames(player);
        AutoTestAssert.Equal(6, flames.Count, "Hexaghost starts combat with 6 Ghostflames");
        AutoTestAssert.True(flames[0] is SearingGhostflameOrb, "Slot 0 is Searing");
        AutoTestAssert.True(flames[1] is CrushingGhostflameOrb, "Slot 1 is Crushing");
        AutoTestAssert.True(flames[2] is BolsteringGhostflameOrb, "Slot 2 is Bolstering");
        AutoTestAssert.True(flames[3] is SearingGhostflameOrb, "Slot 3 is Searing");
        AutoTestAssert.True(flames[4] is CrushingGhostflameOrb, "Slot 4 is Crushing");
        AutoTestAssert.True(flames[5] is InfernoGhostflameOrb, "Slot 5 is Inferno");

        var orbNodes = new List<NOrb>();
        DownfallAutoTestRunner.CollectNodes(orbManager, orbNodes);
        var slot0Node = AutoTestAssert.NotNull(
            orbNodes.FirstOrDefault(n => ReferenceEquals(n.Model, flames[0])),
            "Live NOrb mounted for Slot 0 SearingGhostflameOrb");

        var slot0Sprites = new List<Sprite2D>();
        DownfallAutoTestRunner.CollectNodes(slot0Node, slot0Sprites);
        var slot0Sprite = AutoTestAssert.NotNull(slot0Sprites.FirstOrDefault(), "Slot 0 Ghostflame Sprite2D");
        AutoTestAssert.Equal(
            "res://Downfall/images/hexaghost/orbs/searing_ghostflame_orb.png",
            slot0Sprite.Texture?.ResourcePath,
            "Unignited SearingGhostflameOrb uses dim texture");

        await HexaMechanics.IgniteAsync(ctx, player, flames[0]);
        AutoTestAssert.Equal(
            "res://Downfall/images/hexaghost/orbs/searing_ghostflame_orb_bright.png",
            slot0Sprite.Texture?.ResourcePath,
            "Ignited SearingGhostflameOrb live-swapped to bright texture");

        // Verify in-place ReplaceGhostflame preserves exact slot 1 index and updates mounted NOrb!
        var replacement = (BolsteringGhostflameOrb)ModelDb.Orb<BolsteringGhostflameOrb>().ToMutable();
        HexaMechanics.ReplaceGhostflame(player, 1, replacement);
        var flamesAfter = HexaMechanics.GetGhostflames(player);
        AutoTestAssert.Equal(6, flamesAfter.Count, "In-place replacement keeps 6 Ghostflames");
        AutoTestAssert.True(ReferenceEquals(flamesAfter[1], replacement), "Slot 1 replaced in-place");
        AutoTestAssert.True(flamesAfter[5] is InfernoGhostflameOrb, "Slot 5 Inferno untouched by slot 1 replacement");

        orbNodes.Clear();
        DownfallAutoTestRunner.CollectNodes(orbManager, orbNodes);
        AutoTestAssert.True(
            orbNodes.Any(n => ReferenceEquals(n.Model, replacement)),
            "Live NOrbManager updated to mount replacement GhostflameOrb in-place");
    }

    internal static async Task TestStarterGhostflamesAndAfterlife()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHexaghost>("HEXA_CORE");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive), "Alive enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        // 1. Character stats, starting deck, starter relic & Touch of Orobas upgrade
        AutoTestAssert.Equal(66, player.Creature.MaxHp, "Hexaghost starts with 66 Max HP");
        AutoTestAssert.Equal(11, player.Deck.Cards.Count, "Hexaghost starts with 11 cards");
        var spiritBrand = AutoTestAssert.NotNull(player.Relics.OfType<SpiritBrand>().FirstOrDefault(), "Starts with SpiritBrand");
        AutoTestAssert.Equal(RelicRarity.Starter, spiritBrand.Rarity, "SpiritBrand is Starter rarity");
        var upgrade = AutoTestAssert.NotNull(spiritBrand.GetUpgradeReplacement(), "SpiritBrand has Orobas upgrade");
        AutoTestAssert.True(upgrade is UnbrokenSoul, "SpiritBrand upgrades to UnbrokenSoul");
        AutoTestAssert.Equal(RelicRarity.Starter, upgrade.Rarity, "UnbrokenSoul is Starter rarity");

        // Keep enemy healthy throughout starter test
        await CreatureCmd.SetMaxAndCurrentHp(enemy, 300);

        // 2. Searing Ghostflame ignition via 2 Attacks + SpiritBrand 1st-ignition-per-turn Block
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "Starts on Slot 0 (Searing)");
        pcs.Energy = 10;
        var strike1 = combatState.CreateCard<HexaStrike>(player);
        var strike2 = combatState.CreateCard<HexaStrike>(player);
        await CardPileCmd.Add(strike1, PileType.Hand);
        await CardPileCmd.Add(strike2, PileType.Hand);

        await PlayCardAsync(strike1, enemy);
        var slot0 = HexaMechanics.GetGhostflames(player)[0];
        AutoTestAssert.True(!slot0.IsIgnited, "1 Attack does not ignite Searing");
        AutoTestAssert.Equal(1, slot0.TriggerProgress, "Searing TriggerProgress == 1 after 1 Attack");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<SoulburnPower>(), "No Soulburn before ignition");

        // Verify partial TriggerProgress is preserved across Advance -> Retract onto an UNIGNITED flame!
        await HexaMechanics.AdvanceAsync(ctx, player);
        AutoTestAssert.Equal(1, HexaMechanics.GetActiveSlotIndex(player), "Advanced away from partially progressed Slot 0");
        await HexaMechanics.RetractAsync(ctx, player);
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "Retracted back to Slot 0");
        AutoTestAssert.Equal(1, slot0.TriggerProgress, "Unignited Searing preserved TriggerProgress == 1 after Advance+Retract");

        await PlayCardAsync(strike2, enemy);
        AutoTestAssert.True(slot0.IsIgnited, "2nd Attack ignites Searing");
        AutoTestAssert.Equal(0, slot0.TriggerProgress, "Searing TriggerProgress resets on ignite");
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<SoulburnPower>(), "Searing applied 3x2 = 6 Soulburn");
        AutoTestAssert.Equal(4, player.Creature.Block, "SpiritBrand granted 4 Block on 1st ignition");

        // 3. Kindle (SkipABeat) unupgraded vs upgraded + SpiritBrand once-per-turn cap
        var kindle = combatState.CreateCard<SkipABeat>(player);
        await CardPileCmd.Add(kindle, PileType.Hand);
        await PlayCardAsync(kindle);
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<SoulburnPower>(), "Unupgraded Kindle does not re-ignite already Ignited flame");

        var kindlePlus = combatState.CreateCard<SkipABeat>(player);
        CardCmd.Upgrade(kindlePlus);
        await CardPileCmd.Add(kindlePlus, PileType.Hand);
        await PlayCardAsync(kindlePlus);
        AutoTestAssert.Equal(12, enemy.GetPowerAmount<SoulburnPower>(), "Kindle+ extinguished and re-ignited Searing (+6 Soulburn)");
        AutoTestAssert.Equal(4, player.Creature.Block, "SpiritBrand only triggers once per turn");

        // 4. End turn while Ignited -> auto-Advances to Slot 1 (Crushing)
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(1, HexaMechanics.GetActiveSlotIndex(player), "Auto-advanced to Slot 1 (Crushing) at end of Round 1");
        AutoTestAssert.True(!HexaMechanics.GetGhostflames(player)[1].IsIgnited, "Entered Slot 1 is unignited");

        // 5. Crushing progress persists across turns when NOT ignited!
        pcs.Energy = 10;
        var defend1 = combatState.CreateCard<HexaDefend>(player);
        await CardPileCmd.Add(defend1, PileType.Hand);
        await PlayCardAsync(defend1);
        var slot1 = HexaMechanics.GetGhostflames(player)[1];
        AutoTestAssert.Equal(1, slot1.TriggerProgress, "1 Skill sets Crushing progress to 1");
        AutoTestAssert.True(!slot1.IsIgnited, "1 Skill does not ignite Crushing");

        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(1, HexaMechanics.GetActiveSlotIndex(player), "Unignited Ghostflame does NOT auto-advance at end of turn");
        AutoTestAssert.Equal(1, slot1.TriggerProgress, "Crushing TriggerProgress persisted across turn end");

        // 2nd Skill on Round 3 via unupgraded Kindle double-ignites Crushing (1/2 -> ignite via Skill play + ignite via Kindle)!
        pcs.Energy = 10;
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);
        int enemyHpBeforeCrush = enemy.CurrentHp;
        var kindleOnCrushing = combatState.CreateCard<SkipABeat>(player);
        await CardPileCmd.Add(kindleOnCrushing, PileType.Hand);
        await PlayCardAsync(kindleOnCrushing);
        AutoTestAssert.True(slot1.IsIgnited, "Unupgraded Kindle on Crushing 1/2 ignited Crushing");
        AutoTestAssert.Equal(enemyHpBeforeCrush - 12, enemy.CurrentHp, "Unupgraded Kindle on Crushing 1/2 double-ignited for 6 + 6 = 12 damage");

        // 6. Float (unupgraded advances to Slot 2 Bolstering) + Float+ (Retract / Stay / Advance choices)
        var floatCard = combatState.CreateCard<Float>(player);
        await CardPileCmd.Add(floatCard, PileType.Hand);
        await PlayCardAsync(floatCard);
        AutoTestAssert.Equal(2, HexaMechanics.GetActiveSlotIndex(player), "Float advanced to Slot 2 (Bolstering)");

        var floatPlus = combatState.CreateCard<Float>(player);
        CardCmd.Upgrade(floatPlus);
        await CardPileCmd.Add(floatPlus, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is HexaRetractChoice))
        {
            await PlayCardAsync(floatPlus);
        }
        AutoTestAssert.Equal(1, HexaMechanics.GetActiveSlotIndex(player), "Float+ Retract moved back to Slot 1");
        AutoTestAssert.True(!slot1.IsIgnited, "Retracting onto Slot 1 extinguished it");

        // Advance back to Slot 2 (Bolstering) using Float+ Advance choice
        var floatPlus2 = combatState.CreateCard<Float>(player);
        CardCmd.Upgrade(floatPlus2);
        await CardPileCmd.Add(floatPlus2, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is HexaAdvanceChoice))
        {
            await PlayCardAsync(floatPlus2);
        }
        AutoTestAssert.Equal(2, HexaMechanics.GetActiveSlotIndex(player), "Float+ Advance moved to Slot 2 (Bolstering)");

        // 7. Bolstering + EnhancePower (Intensity positive & negative) + FlameAffectAllEnemiesPower (repeats Bolstering)
        await PowerCmd.Apply<EnhancePower>(ctx, player.Creature, -10m, player.Creature, null);
        AutoTestAssert.Equal(-10, HexaMechanics.GetIntensity(player), "EnhancePower allows negative values");
        var slot2 = HexaMechanics.GetGhostflames(player)[2];
        AutoTestAssert.Equal(0m, slot2.PassiveVal, "Negative Intensity clamps Bolstering Block preview to 0");

        await PowerCmd.Apply<EnhancePower>(ctx, player.Creature, 12m, player.Creature, null);
        AutoTestAssert.Equal(2, HexaMechanics.GetIntensity(player), "Intensity now +2");
        await PowerCmd.Apply<FlameAffectAllEnemiesPower>(ctx, player.Creature, 2m, player.Creature, null);

        int blockBeforeBolster = player.Creature.Block;
        int strBeforeBolster = player.Creature.GetPowerAmount<StrengthPower>();
        var inflame = combatState.CreateCard<Inflame>(player);
        await CardPileCmd.Add(inflame, PileType.Hand);
        await PlayCardAsync(inflame);
        AutoTestAssert.True(slot2.IsIgnited, "Playing Power ignited Bolstering");
        // Inflame grants +2 Strength; Bolstering repeats x2 with FlameAffectAllEnemiesPower(2) -> +2 Strength, +(4+2)*2 = +12 Block!
        AutoTestAssert.Equal(strBeforeBolster + 4, player.Creature.GetPowerAmount<StrengthPower>(), "Bolstering x2 (+2 Str) + Inflame (+2 Str)");
        AutoTestAssert.Equal(blockBeforeBolster + 12, player.Creature.Block, "Bolstering x2 granted 2 * (4+2) = 12 Block");

        // Also verify FlameAffectAllEnemiesPower(2) on offensive Crushing Ghostflame (2 hits x 2 repeats x (3+2) = 20 damage)!
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);
        int hpBeforeAoeCrush = enemy.CurrentHp;
        await HexaMechanics.IgniteAsync(ctx, player, slot1, extinguishFirstIfIgnited: true);
        AutoTestAssert.Equal(hpBeforeAoeCrush - 20, enemy.CurrentHp, "FlameAffectAllEnemiesPower(2) + Intensity(+2) made Crushing deal 2 * 2 * 5 = 20 damage");

        // Remove FlameAffectAllEnemiesPower & reset Intensity to 0 for clean Inferno test
        await PowerCmd.Remove<FlameAffectAllEnemiesPower>(player.Creature);
        await PowerCmd.Remove<EnhancePower>(player.Creature);

        // 8. Sear Ethereal Afterlife (on Exhaust) + SoulburnPower.DetonateAsync(leaveOne: true)
        await PowerCmd.Remove<SoulburnPower>(enemy);
        var sear = combatState.CreateCard<Sear>(player);
        CardCmd.Upgrade(sear); // 7 dmg, 7 Soulburn
        await CardPileCmd.Add(sear, PileType.Hand);
        await CardCmd.Exhaust(ctx, sear, causedByEthereal: false);
        AutoTestAssert.Equal(7, enemy.GetPowerAmount<SoulburnPower>(), "Exhausting Sear+ triggered Afterlife for 7 Soulburn");

        var sbPower = AutoTestAssert.NotNull(enemy.GetPower<SoulburnPower>(), "Enemy has SoulburnPower");
        sbPower.TurnsRemaining = 2;
        int hpBeforeDetonate = enemy.CurrentHp;
        await sbPower.DetonateAsync(ctx, leaveOne: true);
        AutoTestAssert.Equal(hpBeforeDetonate - 6, enemy.CurrentHp, "DetonateAsync(leaveOne: true) dealt Amount - 1 = 6 Soulburn damage");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<SoulburnPower>(), "DetonateAsync(leaveOne: true) left 1 Soulburn stack");
        AutoTestAssert.Equal(2, enemy.GetPower<SoulburnPower>()!.TurnsRemaining, "DetonateAsync(leaveOne: true) preserved TurnsRemaining");

        // At Amount == 1, DetonateAsync(leaveOne: true) must deal 0 damage and leave 1 stack intact!
        int hpAtOneStack = enemy.CurrentHp;
        await enemy.GetPower<SoulburnPower>()!.DetonateAsync(ctx, leaveOne: true);
        AutoTestAssert.Equal(hpAtOneStack, enemy.CurrentHp, "DetonateAsync(leaveOne: true) at Amount==1 dealt 0 damage");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<SoulburnPower>(), "DetonateAsync(leaveOne: true) at Amount==1 kept 1 stack");

        // 9. UnbrokenSoul (Orobas upgrade) triggers on EVERY ignition in the same turn + Inferno 6/6 bonus!
        await RelicCmd.Remove(spiritBrand);
        await RelicCmd.Obtain<UnbrokenSoul>(player);
        var flames = HexaMechanics.GetGhostflames(player);
        for (int i = 0; i < 6; i++)
            await HexaMechanics.ExtinguishAsync(ctx, player, flames[i]);

        int blockBeforeUnbroken = player.Creature.Block;
        for (int i = 0; i < 5; i++)
            await HexaMechanics.IgniteAsync(ctx, player, flames[i]);
        // 5 ignitions * 4 Block from UnbrokenSoul + 4 Block from Bolstering (slot 2) = 24 Block!
        AutoTestAssert.Equal(blockBeforeUnbroken + 24, player.Creature.Block, "UnbrokenSoul granted 4 Block on every ignition (+4 from Bolstering)");

        // Move active slot to 5 (Inferno) without extinguishing 0..4, then ignite Inferno by spending 3 Energy!
        HexaCombatState.Get(combatState).ActiveSlotIndex = 5;
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);
        int hpBeforeInferno = enemy.CurrentHp;
        // Play 3x 1-cost cards to trigger Inferno via EnergySpent (clear hand first so all 3 fit)
        foreach (var existing in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(existing, PileType.Discard);
        pcs.Energy = 10;
        var sA = combatState.CreateCard<HexaDefend>(player);
        var sB = combatState.CreateCard<HexaDefend>(player);
        var sC = combatState.CreateCard<HexaDefend>(player);
        await CardPileCmd.Add(sA, PileType.Hand);
        await CardPileCmd.Add(sB, PileType.Hand);
        await CardPileCmd.Add(sC, PileType.Hand);
        await PlayCardAsync(sA);
        await PlayCardAsync(sB);
        AutoTestAssert.True(!flames[5].IsIgnited, "2 Energy spent does not ignite Inferno");
        await PlayCardAsync(sC);

        AutoTestAssert.True(flames[5].IsIgnited, "3rd Energy spent ignited Inferno");
        AutoTestAssert.Equal(hpBeforeInferno - 24, enemy.CurrentHp, "Inferno dealt 4 * 6 = 24 damage with all 6 Ghostflames ignited");
        for (int i = 0; i < 5; i++)
            AutoTestAssert.True(!flames[i].IsIgnited, $"Inferno extinguished other Ghostflame slot {i}");
        AutoTestAssert.Equal(2, HexaMechanics.GetIntensity(player), "6/6 Inferno ignition granted +2 Intensity");

        // End turn -> Inferno on slot 5 auto-extinguishes AND advances to slot 0!
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 4);
        AutoTestAssert.True(!flames[5].IsIgnited, "Inferno on slot 5 auto-extinguished at end of turn");
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "Ignited slot 5 auto-advanced to slot 0 at end of turn");
    }

    internal static async Task TestCommonCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHexaghost>("HEXA_COMMON");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive), "Alive enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 500);
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);

        async Task ClearHandAndRefillAsync()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = 20;
        }

        // 1. AdvancingGuard: 8 Block + Advance (Slot 0 -> Slot 1)
        await ClearHandAndRefillAsync();
        int blockBefore = player.Creature.Block;
        var advGuard = combatState.CreateCard<AdvancingGuard>(player);
        await CardPileCmd.Add(advGuard, PileType.Hand);
        await PlayCardAsync(advGuard);
        AutoTestAssert.Equal(blockBefore + 8, player.Creature.Block, "AdvancingGuard granted 8 Block");
        AutoTestAssert.Equal(1, HexaMechanics.GetActiveSlotIndex(player), "AdvancingGuard advanced to Slot 1");

        // 2. Again (Fleeting Flash): Ignite active (Crushing -> 6 dmg), then Extinguish it!
        await ClearHandAndRefillAsync();
        int hpBeforeAgain = enemy.CurrentHp;
        var again = combatState.CreateCard<Again>(player);
        CardCmd.Upgrade(again); // 0 cost
        AutoTestAssert.Equal(0, again.EnergyCost.GetWithModifiers(CostModifiers.All), "Again+ costs 0");
        await CardPileCmd.Add(again, PileType.Hand);
        await PlayCardAsync(again);
        AutoTestAssert.Equal(hpBeforeAgain - 6, enemy.CurrentHp, "Again ignited Crushing for 6 damage");
        AutoTestAssert.True(!HexaMechanics.GetActiveGhostflame(player)!.IsIgnited, "Again extinguished Crushing afterward");

        // 3. BacktrackSmack: 6x2 = 12 damage + Retract (Slot 1 -> Slot 0)
        await ClearHandAndRefillAsync();
        int hpBeforeBacktrack = enemy.CurrentHp;
        var backtrack = combatState.CreateCard<BacktrackSmack>(player);
        await CardPileCmd.Add(backtrack, PileType.Hand);
        await PlayCardAsync(backtrack, enemy);
        AutoTestAssert.Equal(hpBeforeBacktrack - 12, enemy.CurrentHp, "BacktrackSmack dealt 6x2 = 12 damage");
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "BacktrackSmack retracted to Slot 0 (Searing)");

        // 4. BurningTouch: 8 Soulburn (once without Soulburn), then 16 Soulburn (twice when enemy already has Soulburn!)
        await PowerCmd.Remove<SoulburnPower>(enemy);
        await ClearHandAndRefillAsync();
        var bt1 = combatState.CreateCard<BurningTouch>(player);
        await CardPileCmd.Add(bt1, PileType.Hand);
        await PlayCardAsync(bt1, enemy);
        AutoTestAssert.Equal(8, enemy.GetPowerAmount<SoulburnPower>(), "BurningTouch applied 8 Soulburn to unburned target");

        var bt2 = combatState.CreateCard<BurningTouch>(player);
        await CardPileCmd.Add(bt2, PileType.Hand);
        await PlayCardAsync(bt2, enemy);
        AutoTestAssert.Equal(24, enemy.GetPowerAmount<SoulburnPower>(), "BurningTouch applied 8x2 = 16 Soulburn to already-burned target (8 -> 24)");

        // 5. PhantomFireball+ (detonate all but 1) and unupgraded PhantomFireball (detonate remaining 1)
        await ClearHandAndRefillAsync();
        int hpBeforePf = enemy.CurrentHp;
        var pfPlus = combatState.CreateCard<PhantomFireball>(player);
        CardCmd.Upgrade(pfPlus); // 6 dmg + detonate 23
        await CardPileCmd.Add(pfPlus, PileType.Hand);
        await PlayCardAsync(pfPlus, enemy);
        AutoTestAssert.Equal(hpBeforePf - 6 - 23, enemy.CurrentHp, "PhantomFireball+ dealt 6 dmg + 23 Soulburn detonation");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<SoulburnPower>(), "PhantomFireball+ left 1 Soulburn");

        int hpBeforePf2 = enemy.CurrentHp;
        var pf = combatState.CreateCard<PhantomFireball>(player); // Attack #2 on Searing -> Searing ignites (+6 Soulburn -> 7 total) in BeforeCardPlayed, then PhantomFireball deals 4 dmg + detonates all 7!
        await CardPileCmd.Add(pf, PileType.Hand);
        await PlayCardAsync(pf, enemy);
        AutoTestAssert.True(HexaMechanics.GetGhostflames(player)[0].IsIgnited, "2nd Attack ignited Slot 0 Searing before PhantomFireball.OnPlay");
        AutoTestAssert.Equal(hpBeforePf2 - 4 - 7, enemy.CurrentHp, "Unupgraded PhantomFireball dealt 4 dmg + detonated 1 + 6 (from Searing) = 7 Soulburn");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<SoulburnPower>(), "Unupgraded PhantomFireball cleared Soulburn after detonation");

        // 6. HauntingEcho: Active Slot 0 (Searing) is Ignited -> deals 7 dmg and re-ignites Searing (+6 Soulburn)!
        await ClearHandAndRefillAsync();
        int burnBeforeEcho = enemy.GetPowerAmount<SoulburnPower>();
        int hpBeforeEcho = enemy.CurrentHp;
        var echo = combatState.CreateCard<HauntingEcho>(player);
        await CardPileCmd.Add(echo, PileType.Hand);
        await PlayCardAsync(echo, enemy);
        AutoTestAssert.Equal(hpBeforeEcho - 7, enemy.CurrentHp, "HauntingEcho dealt 7 damage");
        AutoTestAssert.Equal(burnBeforeEcho + 6, enemy.GetPowerAmount<SoulburnPower>(), "HauntingEcho re-ignited Searing for +6 Soulburn");

        // HauntingEcho when active Ghostflame is UNIGNITED deals 7 damage and does NOT ignite it!
        await HexaMechanics.AdvanceAsync(ctx, player); // Slot 1 Crushing (unignited, needs Skills)
        int hpBeforeEchoMiss = enemy.CurrentHp;
        var echoMiss = combatState.CreateCard<HauntingEcho>(player);
        await CardPileCmd.Add(echoMiss, PileType.Hand);
        await PlayCardAsync(echoMiss, enemy);
        AutoTestAssert.Equal(hpBeforeEchoMiss - 7, enemy.CurrentHp, "HauntingEcho dealt 7 damage on unignited flame");
        AutoTestAssert.True(!HexaMechanics.GetGhostflames(player)[1].IsIgnited, "HauntingEcho did not ignite unignited Crushing");

        // 7. FlameDevourer (Devour Flame): Already on Slot 1 with previous Slot 0 Ignited -> 9 Block + Retract to Slot 0 (extinguishing Slot 0)!
        AutoTestAssert.Equal(1, HexaMechanics.GetActiveSlotIndex(player), "Now on Slot 1 with previous Slot 0 Ignited");
        await ClearHandAndRefillAsync();
        int blockBeforeDevour = player.Creature.Block;
        var devour = combatState.CreateCard<FlameDevourer>(player);
        await CardPileCmd.Add(devour, PileType.Hand);
        await PlayCardAsync(devour);
        AutoTestAssert.Equal(blockBeforeDevour + 9, player.Creature.Block, "FlameDevourer granted 9 Block when previous Ghostflame was Ignited");
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "FlameDevourer retracted to Slot 0");
        AutoTestAssert.True(!HexaMechanics.GetGhostflames(player)[0].IsIgnited, "Retracting onto Slot 0 extinguished it");

        // Playing FlameDevourer when previous (Slot 5) is NOT ignited grants 0 Block and does not retract
        int blockBeforeDevourMiss = player.Creature.Block;
        var devourMiss = combatState.CreateCard<FlameDevourer>(player);
        await CardPileCmd.Add(devourMiss, PileType.Hand);
        await PlayCardAsync(devourMiss);
        AutoTestAssert.Equal(blockBeforeDevourMiss, player.Creature.Block, "FlameDevourer does nothing when previous Ghostflame is unignited");
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "FlameDevourer did not retract when previous was unignited");

        // 8. Firestarter (5 dmg + 5 Soulburn AoE) + HeatMetal (4 dmg + 4 Soulburn + 1 Vulnerable) + ThermalTransfer (7 dmg + 6 Block if Soulburn)
        await PowerCmd.Remove<SoulburnPower>(enemy);
        await ClearHandAndRefillAsync();
        int hpBeforeFire = enemy.CurrentHp;
        var firestarter = combatState.CreateCard<Firestarter>(player);
        await CardPileCmd.Add(firestarter, PileType.Hand);
        await PlayCardAsync(firestarter);
        AutoTestAssert.Equal(hpBeforeFire - 5, enemy.CurrentHp, "Firestarter dealt 5 damage");
        AutoTestAssert.Equal(5, enemy.GetPowerAmount<SoulburnPower>(), "Firestarter applied 5 Soulburn");

        int hpBeforeHeat = enemy.CurrentHp;
        var heatMetal = combatState.CreateCard<HeatMetal>(player);
        await CardPileCmd.Add(heatMetal, PileType.Hand);
        await PlayCardAsync(heatMetal, enemy);
        AutoTestAssert.Equal(hpBeforeHeat - 4, enemy.CurrentHp, "HeatMetal dealt 4 damage");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "HeatMetal applied 1 Vulnerable");
        // Note: Firestarter + HeatMetal ignited Searing (+6 Soulburn) + HeatMetal (+4) -> 5 + 6 + 4 = 15 Soulburn!
        AutoTestAssert.Equal(15, enemy.GetPowerAmount<SoulburnPower>(), "HeatMetal + Searing ignition totaled 15 Soulburn");

        await PowerCmd.Remove<VulnerablePower>(enemy);
        int blockBeforeThermal = player.Creature.Block;
        int hpBeforeThermal = enemy.CurrentHp;
        var thermal = combatState.CreateCard<ThermalTransfer>(player);
        await CardPileCmd.Add(thermal, PileType.Hand);
        await PlayCardAsync(thermal, enemy);
        AutoTestAssert.Equal(hpBeforeThermal - 7, enemy.CurrentHp, "ThermalTransfer dealt 7 damage");
        AutoTestAssert.Equal(blockBeforeThermal + 6, player.Creature.Block, "ThermalTransfer granted 6 Block against Soulburned target");

        // ThermalTransfer without Soulburn: deals 7 damage and grants 0 Block!
        await PowerCmd.Remove<SoulburnPower>(enemy);
        int blockBeforeThermalMiss = player.Creature.Block;
        int hpBeforeThermalMiss = enemy.CurrentHp;
        var thermalMiss = combatState.CreateCard<ThermalTransfer>(player);
        await CardPileCmd.Add(thermalMiss, PileType.Hand);
        await PlayCardAsync(thermalMiss, enemy);
        AutoTestAssert.Equal(hpBeforeThermalMiss - 7, enemy.CurrentHp, "ThermalTransfer without Soulburn dealt 7 damage");
        AutoTestAssert.Equal(blockBeforeThermalMiss, player.Creature.Block, "ThermalTransfer without Soulburn granted 0 Block");

        // 9. InspiringFlames (Stoke the Fire): 7 Block + upgrades 1 card in Hand per Ignited Ghostflame!
        await ClearHandAndRefillAsync();
        var dummyStrike = combatState.CreateCard<HexaStrike>(player);
        var inspire = combatState.CreateCard<InspiringFlames>(player);
        await CardPileCmd.Add(dummyStrike, PileType.Hand);
        await CardPileCmd.Add(inspire, PileType.Hand);
        AutoTestAssert.True(!dummyStrike.IsUpgraded, "Dummy HexaStrike starts unupgraded");
        AutoTestAssert.True(HexaMechanics.GetIgnitedCount(player) >= 1, "At least 1 Ghostflame is Ignited");
        int blockBeforeInspire = player.Creature.Block;
        await PlayCardAsync(inspire);
        AutoTestAssert.Equal(blockBeforeInspire + 7, player.Creature.Block, "InspiringFlames granted 7 Block");
        AutoTestAssert.True(dummyStrike.IsUpgraded, "InspiringFlames upgraded HexaStrike in Hand");

        // 10. GhostLash: 8 base + 3 per other Ethereal card in Hand on play; Afterlife deals base 8 only (no Hand bonus)!
        await ClearHandAndRefillAsync();
        var eth1 = combatState.CreateCard<Sear>(player);
        var eth2 = combatState.CreateCard<Sear>(player);
        var ghostLash = combatState.CreateCard<GhostLash>(player);
        await CardPileCmd.Add(eth1, PileType.Hand);
        await CardPileCmd.Add(eth2, PileType.Hand);
        await CardPileCmd.Add(ghostLash, PileType.Hand);
        int hpBeforeLash = enemy.CurrentHp;
        await PlayCardAsync(ghostLash, enemy);
        AutoTestAssert.Equal(hpBeforeLash - 14, enemy.CurrentHp, "GhostLash dealt 8 + 2*3 = 14 damage with 2 other Ethereals in Hand");

        var ghostLash2 = combatState.CreateCard<GhostLash>(player);
        await CardPileCmd.Add(ghostLash2, PileType.Hand);
        int hpBeforeLashAfterlife = enemy.CurrentHp;
        await CardCmd.Exhaust(ctx, ghostLash2, causedByEthereal: true);
        AutoTestAssert.Equal(hpBeforeLashAfterlife - 8, enemy.CurrentHp, "GhostLash Afterlife dealt base 8 damage (without Hand bonus)");

        // 11. NightmareGuise + ShadowGuise & NightmareStrike + ShadowStrike (both Play and Afterlife!)
        await ClearHandAndRefillAsync();
        var ngPlay = combatState.CreateCard<NightmareGuise>(player);
        await CardPileCmd.Add(ngPlay, PileType.Hand);
        int blockBeforeNgPlay = player.Creature.Block;
        await PlayCardAsync(ngPlay);
        AutoTestAssert.Equal(blockBeforeNgPlay + 4, player.Creature.Block, "NightmareGuise OnPlay granted 4 Block");
        var sgUnupg = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<ShadowGuise>().FirstOrDefault(), "NightmareGuise OnPlay added ShadowGuise");
        AutoTestAssert.True(!sgUnupg.IsUpgraded, "Unupgraded NightmareGuise added unupgraded ShadowGuise");
        await CardPileCmd.Add(sgUnupg, PileType.Discard);

        var ngPlus = combatState.CreateCard<NightmareGuise>(player);
        CardCmd.Upgrade(ngPlus);
        await CardPileCmd.Add(ngPlus, PileType.Hand);
        await CardCmd.Exhaust(ctx, ngPlus, causedByEthereal: true);
        var shadowGuise = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<ShadowGuise>().FirstOrDefault(), "NightmareGuise+ Afterlife added ShadowGuise+");
        AutoTestAssert.True(shadowGuise.IsUpgraded, "ShadowGuise is upgraded");
        int blockBeforeSg = player.Creature.Block;
        await PlayCardAsync(shadowGuise);
        AutoTestAssert.Equal(blockBeforeSg + 6, player.Creature.Block, "ShadowGuise+ granted 6 Block");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(shadowGuise), "ShadowGuise exhausted on play");

        var ns = combatState.CreateCard<NightmareStrike>(player);
        AutoTestAssert.True(ns.Tags.Contains(CardTag.Strike), "NightmareStrike has CardTag.Strike");
        await CardPileCmd.Add(ns, PileType.Hand);
        int hpBeforeNs = enemy.CurrentHp;
        await PlayCardAsync(ns, enemy);
        AutoTestAssert.Equal(hpBeforeNs - 4, enemy.CurrentHp, "NightmareStrike dealt 4 damage");
        var shadowStrike = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<ShadowStrike>().FirstOrDefault(), "NightmareStrike added ShadowStrike to Hand");
        AutoTestAssert.True(shadowStrike.Tags.Contains(CardTag.Strike), "ShadowStrike has CardTag.Strike");
        int hpBeforeSs = enemy.CurrentHp;
        await PlayCardAsync(shadowStrike, enemy);
        AutoTestAssert.Equal(hpBeforeSs - 4, enemy.CurrentHp, "ShadowStrike dealt 4 damage");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(shadowStrike), "ShadowStrike exhausted on play");

        var nsPlus = combatState.CreateCard<NightmareStrike>(player);
        CardCmd.Upgrade(nsPlus);
        await CardPileCmd.Add(nsPlus, PileType.Hand);
        await CardCmd.Exhaust(ctx, nsPlus, causedByEthereal: true);
        var shadowStrikePlus = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<ShadowStrike>().FirstOrDefault(), "NightmareStrike+ Afterlife added ShadowStrike+");
        AutoTestAssert.True(shadowStrikePlus.IsUpgraded, "ShadowStrike+ is upgraded");
        int hpBeforeSsPlus = enemy.CurrentHp;
        await PlayCardAsync(shadowStrikePlus, enemy);
        AutoTestAssert.Equal(hpBeforeSsPlus - 6, enemy.CurrentHp, "ShadowStrike+ dealt 6 damage");

        // 12. SpectersWail: 4x2 = 8 on Play, 4x1 = 4 on Afterlife!
        await ClearHandAndRefillAsync();
        var wail1 = combatState.CreateCard<SpectersWail>(player);
        await CardPileCmd.Add(wail1, PileType.Hand);
        int hpBeforeWail = enemy.CurrentHp;
        await PlayCardAsync(wail1);
        AutoTestAssert.Equal(hpBeforeWail - 8, enemy.CurrentHp, "SpectersWail dealt 4x2 = 8 AoE damage on play");

        var wail2 = combatState.CreateCard<SpectersWail>(player);
        await CardPileCmd.Add(wail2, PileType.Hand);
        int hpBeforeWailAfterlife = enemy.CurrentHp;
        await CardCmd.Exhaust(ctx, wail2, causedByEthereal: false);
        AutoTestAssert.Equal(hpBeforeWailAfterlife - 4, enemy.CurrentHp, "SpectersWail Afterlife dealt 4x1 = 4 AoE damage");

        // 13. StrikeFromBeyond: 3 damage + searches past non-Ethereal top card to draw an Ethereal card from Draw Pile
        await ClearHandAndRefillAsync();
        var drawEthereal = combatState.CreateCard<Sear>(player);
        var nonEtherealTop = combatState.CreateCard<HexaDefend>(player);
        await CardPileCmd.Add(drawEthereal, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(nonEtherealTop, PileType.Draw, CardPilePosition.Top);
        var sfb = combatState.CreateCard<StrikeFromBeyond>(player);
        AutoTestAssert.True(sfb.Tags.Contains(CardTag.Strike), "StrikeFromBeyond has CardTag.Strike");
        await CardPileCmd.Add(sfb, PileType.Hand);
        int hpBeforeSfb = enemy.CurrentHp;
        await PlayCardAsync(sfb, enemy);
        AutoTestAssert.Equal(hpBeforeSfb - 3, enemy.CurrentHp, "StrikeFromBeyond dealt 3 damage");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c.Keywords.Contains(CardKeyword.Ethereal)), "StrikeFromBeyond drew an Ethereal card past non-Ethereal top card");
        AutoTestAssert.True(!pcs.Hand.Cards.Contains(nonEtherealTop), "StrikeFromBeyond did not draw the non-Ethereal top card");

        // 14. ShieldOfDarkness & SwordOfDarkness: Scry top 3 -> discarding an Ethereal card Exhausts it and triggers Afterlife!
        await ClearHandAndRefillAsync();
        var topWail = combatState.CreateCard<SpectersWail>(player);
        var topStrike = combatState.CreateCard<HexaStrike>(player);
        await CardPileCmd.Add(topStrike, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(topWail, PileType.Draw, CardPilePosition.Top);

        int hpBeforeShieldScry = enemy.CurrentHp;
        int blockBeforeShield = player.Creature.Block;
        var shieldDark = combatState.CreateCard<ShieldOfDarkness>(player);
        await CardPileCmd.Add(shieldDark, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, topWail) || ReferenceEquals(c, topStrike)))
        {
            await PlayCardAsync(shieldDark);
        }
        AutoTestAssert.Equal(blockBeforeShield + 12, player.Creature.Block, "ShieldOfDarkness granted 12 Block");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(topWail), "ShieldOfDarkness Exhausted scry-discarded Ethereal card");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(topStrike), "ShieldOfDarkness discarded non-Ethereal scry card");
        AutoTestAssert.Equal(hpBeforeShieldScry - 4, enemy.CurrentHp, "Scry-exhausted SpectersWail triggered Afterlife for 4 damage");

        var topSear = combatState.CreateCard<Sear>(player);
        await CardPileCmd.Add(topSear, PileType.Draw, CardPilePosition.Top);
        int hpBeforeSword = enemy.CurrentHp;
        var swordDark = combatState.CreateCard<SwordOfDarkness>(player);
        await CardPileCmd.Add(swordDark, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, topSear)))
        {
            await PlayCardAsync(swordDark, enemy);
        }
        AutoTestAssert.Equal(hpBeforeSword - 14, enemy.CurrentHp, "SwordOfDarkness dealt 14 damage");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(topSear), "SwordOfDarkness Exhausted scry-discarded Ethereal Sear");
    }
}
