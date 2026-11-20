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
using MegaCrit.Sts2.Core.ValueProps;

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

    internal static async Task TestUncommonBatch1InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHexaghost>("HEXA_UNC1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive), "Alive enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 800);
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);

        async Task ClearHandAndRefillAsync()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = 20;
        }

        // 1. BurningQuestion: OnPlay (+3 Str, +1 Dex) & Afterlife (+1 Dex only!)
        await ClearHandAndRefillAsync();
        var bq1 = combatState.CreateCard<BurningQuestion>(player);
        await CardPileCmd.Add(bq1, PileType.Hand);
        await PlayCardAsync(bq1);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<StrengthPower>(), "BurningQuestion OnPlay granted +3 Strength");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DexterityPower>(), "BurningQuestion OnPlay granted +1 Dexterity");

        var bq2 = combatState.CreateCard<BurningQuestion>(player);
        await CardPileCmd.Add(bq2, PileType.Hand);
        await CardCmd.Exhaust(ctx, bq2, causedByEthereal: true);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<StrengthPower>(), "BurningQuestion Afterlife did not grant Strength");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DexterityPower>(), "BurningQuestion Afterlife granted +1 Dexterity");
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<DexterityPower>(player.Creature);

        // 2. EmpoweredFlame: +1 (+2) Intensity
        await ClearHandAndRefillAsync();
        var emp = combatState.CreateCard<EmpoweredFlame>(player);
        CardCmd.Upgrade(emp);
        await CardPileCmd.Add(emp, PileType.Hand);
        await PlayCardAsync(emp);
        AutoTestAssert.Equal(2, HexaMechanics.GetIntensity(player), "EmpoweredFlame+ granted +2 Intensity");

        // 3. CatchUp: Advance to Slot 1 so previous is Slot 0 (Searing); CatchUp ignites Searing twice -> 2 * (3+2)*2 = 20 Soulburn, and Exhausts (CatchUp+ does not)!
        await HexaMechanics.AdvanceAsync(ctx, player);
        await PowerCmd.Remove<SoulburnPower>(enemy);
        await ClearHandAndRefillAsync();
        var catchUp = combatState.CreateCard<CatchUp>(player);
        await CardPileCmd.Add(catchUp, PileType.Hand);
        await PlayCardAsync(catchUp);
        AutoTestAssert.Equal(20, enemy.GetPowerAmount<SoulburnPower>(), "CatchUp ignited previous Searing twice (2 * 5 * 2 = 20 Soulburn)");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(catchUp), "Unupgraded CatchUp exhausted");

        var catchUpPlus = combatState.CreateCard<CatchUp>(player);
        CardCmd.Upgrade(catchUpPlus);
        AutoTestAssert.True(!catchUpPlus.Keywords.Contains(CardKeyword.Exhaust), "CatchUp+ removed Exhaust");

        await PowerCmd.Remove<EnhancePower>(player.Creature);

        // 4. ChargedBarrage & Divider scale with Ignited Ghostflames (currently Slot 0 is Ignited -> 1 ignited -> 2 total hits!)
        await PowerCmd.Remove<SoulburnPower>(enemy);
        await ClearHandAndRefillAsync();
        AutoTestAssert.Equal(1, HexaMechanics.GetIgnitedCount(player), "1 Ghostflame currently ignited");
        var barrage = combatState.CreateCard<ChargedBarrage>(player);
        await CardPileCmd.Add(barrage, PileType.Hand);
        await PlayCardAsync(barrage, enemy);
        // Note: CatchUp + ChargedBarrage are 2 Skills on Slot 1 (Crushing), so ChargedBarrage's play ignited Crushing BEFORE OnPlay -> 2 ignited -> 1+2 = 3 applications * 6 = 18 Soulburn!
        AutoTestAssert.Equal(2, HexaMechanics.GetIgnitedCount(player), "ChargedBarrage (2nd Skill) ignited Slot 1 Crushing -> 2 ignited");
        AutoTestAssert.Equal(18, enemy.GetPowerAmount<SoulburnPower>(), "ChargedBarrage applied 6 * (1 + 2 ignited) = 18 Soulburn");

        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);
        int hpBeforeDiv = enemy.CurrentHp;
        var divider = combatState.CreateCard<Divider>(player);
        await CardPileCmd.Add(divider, PileType.Hand);
        await PlayCardAsync(divider, enemy);
        AutoTestAssert.Equal(hpBeforeDiv - 9, enemy.CurrentHp, "Divider dealt 3 * (1 + 2 ignited) = 9 damage");

        // 5. ExtraCrispy (CrispyPower) triggers ON Soulburn detonation (NOT application)!
        await ClearHandAndRefillAsync();
        var crispy = combatState.CreateCard<ExtraCrispy>(player);
        await CardPileCmd.Add(crispy, PileType.Hand);
        await PlayCardAsync(crispy);
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<CrispyPower>(), "ExtraCrispy applied CrispyPower(12)");
        var existingSb = AutoTestAssert.NotNull(enemy.GetPower<SoulburnPower>(), "Enemy has Soulburn");
        int hpBeforeCrispyDet = enemy.CurrentHp;
        await existingSb.DetonateAsync(ctx, leaveOne: false);
        AutoTestAssert.Equal(hpBeforeCrispyDet - 18, enemy.CurrentHp, "Soulburn detonated for 18 damage");
        AutoTestAssert.Equal(12, enemy.GetPowerAmount<SoulburnPower>(), "CrispyPower re-applied 12 Soulburn after detonation");
        await PowerCmd.Remove<CrispyPower>(player.Creature);

        // 6. HeatCrush (12 + 12 Soulburn = 24 damage) & HeatShield (12 Block + delays Soulburn by 1 turn: 3 -> 4!)
        await ClearHandAndRefillAsync();
        int hpBeforeHc = enemy.CurrentHp;
        var heatCrush = combatState.CreateCard<HeatCrush>(player);
        await CardPileCmd.Add(heatCrush, PileType.Hand);
        await PlayCardAsync(heatCrush, enemy);
        AutoTestAssert.Equal(hpBeforeHc - 24, enemy.CurrentHp, "HeatCrush dealt 12 + 12 Soulburn = 24 damage");

        int blockBeforeHs = player.Creature.Block;
        var heatShield = combatState.CreateCard<HeatShield>(player);
        await CardPileCmd.Add(heatShield, PileType.Hand);
        await PlayCardAsync(heatShield, enemy);
        AutoTestAssert.Equal(blockBeforeHs + 12, player.Creature.Block, "HeatShield granted Block equal to 12 Soulburn");
        AutoTestAssert.Equal(4, enemy.GetPower<SoulburnPower>()!.TurnsRemaining, "HeatShield delayed Soulburn by 1 turn (3 -> 4)");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(heatShield), "Unupgraded HeatShield exhausted");

        // 7. EtherealExpedition: adds a random Ethereal card costing 0 this turn and Exhausts!
        await ClearHandAndRefillAsync();
        var exp = combatState.CreateCard<EtherealExpedition>(player);
        await CardPileCmd.Add(exp, PileType.Hand);
        await PlayCardAsync(exp);
        var generated = AutoTestAssert.NotNull(pcs.Hand.Cards.FirstOrDefault(), "EtherealExpedition added a card to Hand");
        AutoTestAssert.True(generated.Keywords.Contains(CardKeyword.Ethereal), "Generated card is Ethereal");
        AutoTestAssert.Equal(0, generated.EnergyCost.GetWithModifiers(CostModifiers.All), "Generated Ethereal card costs 0 this turn");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(exp), "EtherealExpedition exhausted");

        // 8. GhostOfSpirePast (PastPower) + HexaFastForward: Advance triggers PastPower once per turn!
        await ClearHandAndRefillAsync();
        var pastCard = combatState.CreateCard<GhostOfSpirePast>(player);
        await CardPileCmd.Add(pastCard, PileType.Hand);
        await PlayCardAsync(pastCard);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<PastPower>(), "GhostOfSpirePast applied PastPower(1)");

        // Ensure enough cards in draw pile
        for (int i = 0; i < 5; i++)
            await CardPileCmd.Add(combatState.CreateCard<HexaStrike>(player), PileType.Draw, CardPilePosition.Top);

        int slotBeforeFf = HexaMechanics.GetActiveSlotIndex(player);
        var ff = combatState.CreateCard<HexaFastForward>(player);
        await CardPileCmd.Add(ff, PileType.Hand);
        await PlayCardAsync(ff);
        // FastForward drew 2 cards + Advanced -> PastPower drew 1 more = 3 cards in hand!
        AutoTestAssert.Equal((slotBeforeFf + 1) % 6, HexaMechanics.GetActiveSlotIndex(player), "HexaFastForward advanced 1 slot");
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "HexaFastForward (2) + PastPower (1) drew 3 cards");

        // 2nd Advance this turn should NOT trigger PastPower(1) again!
        int handBefore2ndAdv = pcs.Hand.Cards.Count;
        await HexaMechanics.AdvanceAsync(ctx, player, isEndTurn: false);
        AutoTestAssert.Equal(handBefore2ndAdv, pcs.Hand.Cards.Count, "PastPower(1) did not trigger a 2nd time this turn");

        // End-of-turn auto-Advance (isEndTurn: true) must NOT trigger PastPower even with fresh capacity!
        await PowerCmd.Apply<PastPower>(ctx, player.Creature, 2m, player.Creature, null); // Amount=3, _activationsThisTurn=2 -> 1 remaining
        int handBeforeEndTurnAdv = pcs.Hand.Cards.Count;
        await HexaMechanics.AdvanceAsync(ctx, player, isEndTurn: true);
        AutoTestAssert.Equal(handBeforeEndTurnAdv, pcs.Hand.Cards.Count, "PastPower ignored end-of-turn Advance (isEndTurn: true)");

        // Retract DOES trigger PastPower when capacity remains (2 < 3)!
        int handBeforeRetract = pcs.Hand.Cards.Count;
        await HexaMechanics.RetractAsync(ctx, player);
        AutoTestAssert.Equal(handBeforeRetract + 1, pcs.Hand.Cards.Count, "PastPower triggered draw on Retract");

        // 9. FlameSwitch (Bad Omen): skip path leaves slot unchanged; Bolstering choice replaces slot in-place!
        await ClearHandAndRefillAsync();
        int activeBeforeSwitch = HexaMechanics.GetActiveSlotIndex(player);
        var orbBeforeSkip = HexaMechanics.GetGhostflames(player)[activeBeforeSwitch];
        var flameSwitchSkip = combatState.CreateCard<FlameSwitch>(player);
        await CardPileCmd.Add(flameSwitchSkip, PileType.Hand);
        using (DownfallAutoTestRunner.CancelCardSelection())
        {
            await PlayCardAsync(flameSwitchSkip);
        }
        AutoTestAssert.True(ReferenceEquals(orbBeforeSkip, HexaMechanics.GetGhostflames(player)[activeBeforeSwitch]), "FlameSwitch skip left active Ghostflame unchanged");

        var flameSwitch = combatState.CreateCard<FlameSwitch>(player);
        CardCmd.Upgrade(flameSwitch);
        AutoTestAssert.True(flameSwitch.Keywords.Contains(CardKeyword.Retain), "FlameSwitch+ has Retain");
        await CardPileCmd.Add(flameSwitch, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is HexaBolsteringChoice))
        {
            await PlayCardAsync(flameSwitch);
        }
        AutoTestAssert.True(HexaMechanics.GetGhostflames(player)[activeBeforeSwitch] is BolsteringGhostflameOrb, "FlameSwitch replaced active slot with BolsteringGhostflameOrb");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(flameSwitch), "FlameSwitch exhausted");

        // 10. FlamesFromBeyond: 10x2 = 20 Soulburn on Play, 10x1 = 10 Soulburn on Afterlife!
        await PowerCmd.Remove<SoulburnPower>(enemy);
        await ClearHandAndRefillAsync();
        var ffb1 = combatState.CreateCard<FlamesFromBeyond>(player);
        await CardPileCmd.Add(ffb1, PileType.Hand);
        await PlayCardAsync(ffb1);
        AutoTestAssert.Equal(20, enemy.GetPowerAmount<SoulburnPower>(), "FlamesFromBeyond OnPlay applied 10x2 = 20 Soulburn");

        var ffb2 = combatState.CreateCard<FlamesFromBeyond>(player);
        await CardPileCmd.Add(ffb2, PileType.Hand);
        await CardCmd.Exhaust(ctx, ffb2, causedByEthereal: true);
        AutoTestAssert.Equal(30, enemy.GetPowerAmount<SoulburnPower>(), "FlamesFromBeyond Afterlife applied 10x1 = 10 Soulburn (20 -> 30)");

        // 11. Floatwork: OnPlay (+1 Dex, +2 Plating); non-Ethereal Exhaust (+2 Plating, 0 bonus Block)!
        await ClearHandAndRefillAsync();
        var fw1 = combatState.CreateCard<Floatwork>(player);
        await CardPileCmd.Add(fw1, PileType.Hand);
        await PlayCardAsync(fw1);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DexterityPower>(), "Floatwork OnPlay granted +1 Dexterity");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<PlatingPower>(), "Floatwork OnPlay granted +2 Plating");

        int blockBeforeFwManual = player.Creature.Block;
        var fwManual = combatState.CreateCard<Floatwork>(player);
        await CardPileCmd.Add(fwManual, PileType.Hand);
        await CardCmd.Exhaust(ctx, fwManual, causedByEthereal: false);
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<PlatingPower>(), "Floatwork non-Ethereal Afterlife granted +2 Plating (2 -> 4)");
        AutoTestAssert.Equal(blockBeforeFwManual, player.Creature.Block, "Floatwork non-Ethereal Afterlife granted 0 bonus Block");
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<DexterityPower>(player.Creature);
        await PowerCmd.Remove<PlatingPower>(player.Creature);

        // 12. GhostShield (OnPlay: 6 Block + 1 Blur; Afterlife: 6 Block) & Hexaguard (OnPlay: 6 Block + Draw 2; Afterlife: 6 Block)
        await ClearHandAndRefillAsync();
        int blockBeforeGs = player.Creature.Block;
        var gs = combatState.CreateCard<GhostShield>(player);
        await CardPileCmd.Add(gs, PileType.Hand);
        await PlayCardAsync(gs);
        AutoTestAssert.Equal(blockBeforeGs + 6, player.Creature.Block, "GhostShield granted 6 Block");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<BlurPower>(), "GhostShield granted 1 Blur");

        var gs2 = combatState.CreateCard<GhostShield>(player);
        await CardPileCmd.Add(gs2, PileType.Hand);
        int blockBeforeGsAfter = player.Creature.Block;
        await CardCmd.Exhaust(ctx, gs2, causedByEthereal: false);
        AutoTestAssert.Equal(blockBeforeGsAfter + 6, player.Creature.Block, "GhostShield Afterlife granted 6 Block");

        // Hexaguard OnPlay (6 Block + Draw 2) & Afterlife (6 Block)
        await ClearHandAndRefillAsync();
        for (int i = 0; i < 3; i++)
            await CardPileCmd.Add(combatState.CreateCard<HexaStrike>(player), PileType.Draw, CardPilePosition.Top);
        var hgPlay = combatState.CreateCard<Hexaguard>(player);
        await CardPileCmd.Add(hgPlay, PileType.Hand);
        int blockBeforeHgPlay = player.Creature.Block;
        await PlayCardAsync(hgPlay);
        AutoTestAssert.Equal(blockBeforeHgPlay + 6, player.Creature.Block, "Hexaguard OnPlay granted 6 Block");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Hexaguard OnPlay drew 2 cards");

        var hg = combatState.CreateCard<Hexaguard>(player);
        await CardPileCmd.Add(hg, PileType.Hand);
        int blockBeforeHg = player.Creature.Block;
        await CardCmd.Exhaust(ctx, hg, causedByEthereal: true);
        AutoTestAssert.Equal(blockBeforeHg + 6, player.Creature.Block, "Hexaguard Afterlife granted 6 Block");

        // 13. GhostflameBarrier (Whisper from Beyond): 12 dmg + SoulburnOnExhaustPower(5)
        await PowerCmd.Remove<SoulburnPower>(enemy);
        await ClearHandAndRefillAsync();
        int hpBeforeGfb = enemy.CurrentHp;
        var gfb = combatState.CreateCard<GhostflameBarrier>(player);
        await CardPileCmd.Add(gfb, PileType.Hand);
        await PlayCardAsync(gfb, enemy);
        AutoTestAssert.Equal(hpBeforeGfb - 12, enemy.CurrentHp, "GhostflameBarrier dealt 12 damage");
        AutoTestAssert.Equal(5, enemy.GetPowerAmount<SoulburnOnExhaustPower>(), "GhostflameBarrier applied SoulburnOnExhaustPower(5)");

        var dummyExh = combatState.CreateCard<HexaStrike>(player);
        await CardPileCmd.Add(dummyExh, PileType.Hand);
        await CardCmd.Exhaust(ctx, dummyExh, causedByEthereal: false);
        AutoTestAssert.Equal(5, enemy.GetPowerAmount<SoulburnPower>(), "Exhausting a card triggered SoulburnOnExhaustPower for 5 Soulburn");

        // 14. Haunt (Haunted Hand): OnPlay (Draw 1 + draw until non-Ethereal) & Afterlife (draw until non-Ethereal)
        await ClearHandAndRefillAsync();
        var nonEthPlay = combatState.CreateCard<HexaStrike>(player);
        var ethPlay = combatState.CreateCard<Sear>(player);
        var firstDrawPlay = combatState.CreateCard<HexaDefend>(player);
        await CardPileCmd.Add(nonEthPlay, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(ethPlay, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(firstDrawPlay, PileType.Draw, CardPilePosition.Top);
        var hauntPlay = combatState.CreateCard<Downfall.Hexaghost.Cards.Haunt>(player);
        await CardPileCmd.Add(hauntPlay, PileType.Hand);
        await PlayCardAsync(hauntPlay);
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "Haunt OnPlay drew 1 card + 1 Ethereal + stopped on 1st non-Ethereal (3 cards)");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(nonEthPlay), "Haunt OnPlay stopped on non-Ethereal card");

        await ClearHandAndRefillAsync();
        var nonEth = combatState.CreateCard<HexaStrike>(player);
        var eA = combatState.CreateCard<Sear>(player);
        var eB = combatState.CreateCard<Sear>(player);
        await CardPileCmd.Add(nonEth, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(eB, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(eA, PileType.Draw, CardPilePosition.Top);
        var haunt = combatState.CreateCard<Downfall.Hexaghost.Cards.Haunt>(player);
        await CardPileCmd.Add(haunt, PileType.Hand);
        await CardCmd.Exhaust(ctx, haunt, causedByEthereal: false);
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "Haunt Afterlife drew 2 Ethereals + stopped on 1st non-Ethereal (3 cards)");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(nonEth), "Haunt stopped after drawing the non-Ethereal card");

        // 15. Real End-of-Turn verification:
        // - Hold an Ethereal Floatwork in Hand alongside the 2 Ethereal Sear cards + existing BlurPower!
        // - Verify Floatwork Ethereal Afterlife grants +2 Plating & single +2 Block (no double-Block with PlatingPower),
        //   SoulburnOnExhaustPower triggers on the end-of-turn Ethereal exhausts BEFORE expiring in AfterSideTurnEnd,
        //   and PastPower resets its per-turn counter on Round 2!
        var fwEndTurn = combatState.CreateCard<Floatwork>(player);
        await CardPileCmd.Add(fwEndTurn, PileType.Hand);
        int burnBeforeEndTurn = enemy.GetPowerAmount<SoulburnPower>();
        // Neutralize enemy attack damage via negative Strength so we can assert exact Floatwork Ethereal Block (+2, NOT +4!) through Blur!
        await PowerCmd.Apply<StrengthPower>(ctx, enemy, -99m, player.Creature, null);
        int blockBeforeEndTurn = player.Creature.Block;

        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, 2);

        AutoTestAssert.Equal(blockBeforeEndTurn + 2, player.Creature.Block, "Floatwork Ethereal Afterlife granted exactly +2 Block on turn end (no double-Block with PlatingPower)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<PlatingPower>(), "Floatwork Ethereal Afterlife granted +2 Plating which decremented to 1 at Round 2 start");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<SoulburnOnExhaustPower>(), "SoulburnOnExhaustPower expired after player turn ended");
        // 3 Ethereal cards in hand (eA Sear, eB Sear, fwEndTurn Floatwork) all exhausted at turn end:
        // 2x Sear Afterlife (+10 Soulburn) + 3x SoulburnOnExhaustPower (+15 Soulburn) = +25 Soulburn!
        AutoTestAssert.Equal(burnBeforeEndTurn + 25, enemy.GetPowerAmount<SoulburnPower>(), "SoulburnOnExhaustPower triggered on all 3 end-of-turn Ethereal exhausts (+15) plus 2x Sear Afterlife (+10)");

        // Verify PastPower reset on Round 2: Advance draws 1 card again!
        int handRound2BeforeAdv = pcs.Hand.Cards.Count;
        await HexaMechanics.AdvanceAsync(ctx, player, isEndTurn: false);
        AutoTestAssert.Equal(handRound2BeforeAdv + 1, pcs.Hand.Cards.Count, "PastPower reset on Round 2 and drew 1 card on Advance");
    }

    internal static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHexaghost>("HEXA_UNC2");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive), "Alive enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000);
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);

        async Task ClearHandAndRefillAsync()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = 20;
        }

        // 1. Incineration: 4 dmg + 4 Soulburn x3 (12 dmg, 12 Soulburn)!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<SoulburnPower>(enemy);
        int hpBeforeInc = enemy.CurrentHp;
        var inc = combatState.CreateCard<Incineration>(player);
        await CardPileCmd.Add(inc, PileType.Hand);
        await PlayCardAsync(inc, enemy);
        AutoTestAssert.Equal(hpBeforeInc - 12, enemy.CurrentHp, "Incineration dealt 4x3 = 12 damage");
        AutoTestAssert.Equal(12, enemy.GetPowerAmount<SoulburnPower>(), "Incineration applied 4x3 = 12 Soulburn");

        // 2. RadiantFlame (Radiant Reverb): 18 dmg + removes target's Soulburn + grants +2 Intensity!
        // Note: Incineration + RadiantFlame are 2 Attacks on Slot 0 (Searing) -> Searing ignites (+6 Soulburn) in BeforeCardPlayed, then RadiantFlame removes ALL Soulburn and grants +2 Intensity!
        await ClearHandAndRefillAsync();
        int hpBeforeRad = enemy.CurrentHp;
        var rad = combatState.CreateCard<RadiantFlame>(player);
        await CardPileCmd.Add(rad, PileType.Hand);
        await PlayCardAsync(rad, enemy);
        AutoTestAssert.Equal(hpBeforeRad - 18, enemy.CurrentHp, "RadiantFlame dealt 18 damage");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<SoulburnPower>(), "RadiantFlame removed target Soulburn");
        AutoTestAssert.Equal(2, HexaMechanics.GetIntensity(player), "RadiantFlame granted +2 Intensity");
        await PowerCmd.Remove<EnhancePower>(player.Creature);

        // RadiantFlame without Soulburn deals 18 dmg and grants 0 Intensity
        int hpBeforeRadMiss = enemy.CurrentHp;
        var radMiss = combatState.CreateCard<RadiantFlame>(player);
        await CardPileCmd.Add(radMiss, PileType.Hand);
        await PlayCardAsync(radMiss, enemy);
        AutoTestAssert.Equal(hpBeforeRadMiss - 18, enemy.CurrentHp, "RadiantFlame without Soulburn dealt 18 damage");
        AutoTestAssert.Equal(0, HexaMechanics.GetIntensity(player), "RadiantFlame without Soulburn granted 0 Intensity");

        // 3. RainOfEmbers (X-cost): spend 3 Energy -> 6x3 = 18 dmg + 3 Weak!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<WeakPower>(enemy);
        pcs.Energy = 3;
        int hpBeforeRain = enemy.CurrentHp;
        var rain = combatState.CreateCard<RainOfEmbers>(player);
        await CardPileCmd.Add(rain, PileType.Hand);
        await PlayCardAsync(rain, enemy);
        AutoTestAssert.Equal(0, pcs.Energy, "RainOfEmbers spent all 3 Energy");
        AutoTestAssert.Equal(hpBeforeRain - 18, enemy.CurrentHp, "RainOfEmbers dealt 6x3 = 18 damage");
        AutoTestAssert.Equal(3, enemy.GetPowerAmount<WeakPower>(), "RainOfEmbers applied 3 Weak");
        await PowerCmd.Remove<WeakPower>(enemy);

        // 4. NecessarySacrifice (Worthy Sacrifice): Exhaust 1 Attack + 1 Skill -> creates 1 Skill + 1 Attack (upgraded when Worthy Sacrifice+)!
        await ClearHandAndRefillAsync();
        var sacAtk = combatState.CreateCard<HexaStrike>(player);
        var sacSkl = combatState.CreateCard<HexaDefend>(player);
        var sac = combatState.CreateCard<NecessarySacrifice>(player);
        CardCmd.Upgrade(sac);
        await CardPileCmd.Add(sacAtk, PileType.Hand);
        await CardPileCmd.Add(sacSkl, PileType.Hand);
        await CardPileCmd.Add(sac, PileType.Hand);
        await PlayCardAsync(sac);
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(sacAtk), "NecessarySacrifice exhausted Attack");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(sacSkl), "NecessarySacrifice exhausted Skill");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(sac), "NecessarySacrifice exhausted itself");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "NecessarySacrifice added 2 transformed cards");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c.Type == CardType.Skill), "Exhausted Attack became a Skill");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c.Type == CardType.Attack), "Exhausted Skill became an Attack");
        AutoTestAssert.True(pcs.Hand.Cards.All(c => c.IsUpgraded || !c.IsUpgradable), "NecessarySacrifice+ upgraded generated cards");

        // 5. OpposingAction: Retract (Slot 0 -> Slot 5) + Gain 2 Energy + Exhaust!
        await ClearHandAndRefillAsync();
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "On Slot 0 before OpposingAction");
        pcs.Energy = 1;
        var opp = combatState.CreateCard<OpposingAction>(player);
        await CardPileCmd.Add(opp, PileType.Hand);
        await PlayCardAsync(opp);
        AutoTestAssert.Equal(5, HexaMechanics.GetActiveSlotIndex(player), "OpposingAction retracted to Slot 5");
        AutoTestAssert.Equal(3, pcs.Energy, "OpposingAction granted +2 Energy (1 -> 3)");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(opp), "OpposingAction exhausted");

        // 6. PowerFromBeyond: OnPlay (+1 EnergyNextTurn, +2 DrawNextTurn) & Afterlife (+2 DrawNextTurn only!)
        await ClearHandAndRefillAsync();
        var pfb1 = combatState.CreateCard<PowerFromBeyond>(player);
        CardCmd.Upgrade(pfb1); // +2 Energy next turn, +2 Draw next turn
        await CardPileCmd.Add(pfb1, PileType.Hand);
        await PlayCardAsync(pfb1);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<EnergyNextTurnPower>(), "PowerFromBeyond+ granted 2 EnergyNextTurn");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "PowerFromBeyond+ granted 2 DrawCardsNextTurn");

        var pfb2 = combatState.CreateCard<PowerFromBeyond>(player);
        await CardPileCmd.Add(pfb2, PileType.Hand);
        await CardCmd.Exhaust(ctx, pfb2, causedByEthereal: true);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<EnergyNextTurnPower>(), "PowerFromBeyond Afterlife did not grant EnergyNextTurn");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "PowerFromBeyond Afterlife granted +2 DrawCardsNextTurn (2 -> 4)");

        // 7. Premonition: place non-matching Attack on top of EmpoweredFlame -> choose Power -> auto-plays EmpoweredFlame past Attack!
        await ClearHandAndRefillAsync();
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var drawEmp = combatState.CreateCard<EmpoweredFlame>(player);
        var topAttack = combatState.CreateCard<HexaStrike>(player);
        await CardPileCmd.Add(drawEmp, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(topAttack, PileType.Draw, CardPilePosition.Top);
        var prem = combatState.CreateCard<Premonition>(player);
        await CardPileCmd.Add(prem, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is HexaPremonitionPower))
        {
            await PlayCardAsync(prem);
        }
        AutoTestAssert.Equal(1, HexaMechanics.GetIntensity(player), "Premonition auto-played EmpoweredFlame past non-matching top Attack (+1 Intensity)");
        AutoTestAssert.True(pcs.DrawPile.Cards.Contains(topAttack), "Premonition left non-matching Attack in Draw Pile");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(prem), "Unupgraded Premonition exhausted");

        var premPlus = combatState.CreateCard<Premonition>(player);
        CardCmd.Upgrade(premPlus);
        AutoTestAssert.True(!premPlus.Keywords.Contains(CardKeyword.Exhaust), "Premonition+ removed Exhaust");
        await PowerCmd.Remove<EnhancePower>(player.Creature);

        // 8. RecurringNightmare (Nightmare Vision): Retract + Exhume Ethereal card from Exhaust Pile!
        await ClearHandAndRefillAsync();
        int slotBeforeRn = HexaMechanics.GetActiveSlotIndex(player);
        var exhSear = combatState.CreateCard<Sear>(player);
        await CardPileCmd.Add(exhSear, PileType.Exhaust);
        var rn = combatState.CreateCard<RecurringNightmare>(player);
        await CardPileCmd.Add(rn, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, exhSear)))
        {
            await PlayCardAsync(rn);
        }
        AutoTestAssert.Equal((slotBeforeRn + 5) % 6, HexaMechanics.GetActiveSlotIndex(player), "RecurringNightmare retracted 1 slot");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(exhSear), "RecurringNightmare exhumed Ethereal Sear into Hand");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(rn), "RecurringNightmare exhausted itself");

        // 9. Rewind: Retract + Fetch card from Discard Pile!
        await ClearHandAndRefillAsync();
        int slotBeforeRw = HexaMechanics.GetActiveSlotIndex(player);
        var discCard = combatState.CreateCard<HexaStrike>(player);
        await CardPileCmd.Add(discCard, PileType.Discard);
        var rw = combatState.CreateCard<Rewind>(player);
        await CardPileCmd.Add(rw, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, discCard)))
        {
            await PlayCardAsync(rw);
        }
        AutoTestAssert.Equal((slotBeforeRw + 5) % 6, HexaMechanics.GetActiveSlotIndex(player), "Rewind retracted 1 slot");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(discCard), "Rewind fetched HexaStrike from Discard Pile");

        // 10. SeventhEye: Fetch from Draw Pile + Switch to a random OTHER Ghostflame!
        await ClearHandAndRefillAsync();
        int slotBeforeSe = HexaMechanics.GetActiveSlotIndex(player);
        var drawTarget = combatState.CreateCard<HexaDefend>(player);
        await CardPileCmd.Add(drawTarget, PileType.Draw, CardPilePosition.Top);
        var seventh = combatState.CreateCard<SeventhEye>(player);
        await CardPileCmd.Add(seventh, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, drawTarget)))
        {
            await PlayCardAsync(seventh);
        }
        AutoTestAssert.True(pcs.Hand.Cards.Contains(drawTarget), "SeventhEye fetched HexaDefend from Draw Pile");
        AutoTestAssert.True(HexaMechanics.GetActiveSlotIndex(player) != slotBeforeSe, "SeventhEye switched to a different Ghostflame");

        // 11. SoulSteal (Living Bomb): applies 4 Soulburn + LivingBombPower; detonation consumes LivingBombPower and deals AoE damage!
        await PowerCmd.Remove<SoulburnPower>(enemy);
        await ClearHandAndRefillAsync();
        var livingBomb = combatState.CreateCard<SoulSteal>(player);
        await CardPileCmd.Add(livingBomb, PileType.Hand);
        await PlayCardAsync(livingBomb, enemy);
        AutoTestAssert.Equal(4, enemy.GetPowerAmount<SoulburnPower>(), "SoulSteal applied 4 Soulburn");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<LivingBombPower>(), "SoulSteal applied LivingBombPower");
        int hpBeforeLbDet = enemy.CurrentHp;
        await enemy.GetPower<SoulburnPower>()!.DetonateAsync(ctx, leaveOne: false);
        AutoTestAssert.Equal(hpBeforeLbDet - 4, enemy.CurrentHp, "LivingBomb Soulburn detonation dealt 4 damage");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<LivingBombPower>(), "LivingBombPower removed after detonation");

        // 12. SpectralSpark (Naughty Spirit): returns to Hand and Retracts when active Ghostflame is Ignited; goes to Discard when unignited!
        await ClearHandAndRefillAsync();
        HexaCombatState.Get(combatState).ActiveSlotIndex = 2; // Slot 2 Bolstering
        var slot2 = HexaMechanics.GetGhostflames(player)[2];
        await HexaMechanics.IgniteAsync(ctx, player, slot2, extinguishFirstIfIgnited: true);
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<SoulburnPower>(enemy);
        var spark = combatState.CreateCard<SpectralSpark>(player);
        await CardPileCmd.Add(spark, PileType.Hand);
        await PlayCardAsync(spark, enemy);
        AutoTestAssert.Equal(3, enemy.GetPowerAmount<SoulburnPower>(), "SpectralSpark applied 3 Soulburn");
        AutoTestAssert.Equal(1, HexaMechanics.GetActiveSlotIndex(player), "SpectralSpark retracted from Slot 2 to Slot 1");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(spark), "SpectralSpark returned to Hand after playing on Ignited Ghostflame");

        // Playing SpectralSpark again on unignited Slot 1 does NOT return to Hand and does NOT retract!
        await PlayCardAsync(spark, enemy);
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<SoulburnPower>(), "2nd SpectralSpark applied +3 Soulburn");
        AutoTestAssert.Equal(1, HexaMechanics.GetActiveSlotIndex(player), "SpectralSpark on unignited flame did not retract");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(spark), "SpectralSpark on unignited flame went to Discard");

        // 13. SpectralGrace: Discard 2 cards + Deal 13 damage!
        await ClearHandAndRefillAsync();
        var d1 = combatState.CreateCard<HexaStrike>(player);
        var d2 = combatState.CreateCard<HexaDefend>(player);
        var grace = combatState.CreateCard<SpectralGrace>(player);
        await CardPileCmd.Add(d1, PileType.Hand);
        await CardPileCmd.Add(d2, PileType.Hand);
        await CardPileCmd.Add(grace, PileType.Hand);
        int hpBeforeGrace = enemy.CurrentHp;
        await PlayCardAsync(grace, enemy);
        AutoTestAssert.Equal(hpBeforeGrace - 13, enemy.CurrentHp, "SpectralGrace dealt 13 damage");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(d1) && pcs.DiscardPile.Cards.Contains(d2), "SpectralGrace discarded 2 cards");

        // 14. StepThrough (7 dmg + draw 1 per Ignited Ghostflame) & SpectralAdrenaline (reset wheel to 0, extinguish all ignited, +1 Energy & +1 Draw per extinguished!)
        await ClearHandAndRefillAsync();
        for (int i = 0; i < 6; i++)
            await CardPileCmd.Add(combatState.CreateCard<HexaStrike>(player), PileType.Draw, CardPilePosition.Top);
        var flames = HexaMechanics.GetGhostflames(player);
        for (int i = 0; i < 6; i++)
            await HexaMechanics.ExtinguishAsync(ctx, player, flames[i]);
        await HexaMechanics.IgniteAsync(ctx, player, flames[0]);
        await HexaMechanics.IgniteAsync(ctx, player, flames[3]); // 2 ignited total!
        HexaCombatState.Get(combatState).ActiveSlotIndex = 2; // Bolstering (needs a Power, so Attack/Skill won't ignite it)

        int hpBeforeStep = enemy.CurrentHp;
        var step = combatState.CreateCard<StepThrough>(player);
        await CardPileCmd.Add(step, PileType.Hand);
        await PlayCardAsync(step, enemy);
        AutoTestAssert.Equal(hpBeforeStep - 7, enemy.CurrentHp, "StepThrough dealt 7 damage");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "StepThrough drew 2 cards for 2 Ignited Ghostflames");

        await ClearHandAndRefillAsync();
        pcs.Energy = 2;
        var brightRitual = combatState.CreateCard<SpectralAdrenaline>(player);
        await CardPileCmd.Add(brightRitual, PileType.Hand);
        await PlayCardAsync(brightRitual);
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "SpectralAdrenaline reset Wheel to Slot 0");
        AutoTestAssert.Equal(0, HexaMechanics.GetIgnitedCount(player), "SpectralAdrenaline extinguished all Ignited Ghostflames");
        AutoTestAssert.Equal(2, pcs.Energy, "SpectralAdrenaline cost 2 and refunded +2 Energy for 2 Extinguished flames");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "SpectralAdrenaline drew 2 cards for 2 Extinguished flames");

        // 15. TimeOfNeed: Retain, Exhaust, adds random Power costing 1 less this combat!
        await ClearHandAndRefillAsync();
        var ton = combatState.CreateCard<TimeOfNeed>(player);
        AutoTestAssert.True(ton.Keywords.Contains(CardKeyword.Retain), "TimeOfNeed has Retain");
        await CardPileCmd.Add(ton, PileType.Hand);
        await PlayCardAsync(ton);
        var genPower = AutoTestAssert.NotNull(pcs.Hand.Cards.FirstOrDefault(), "TimeOfNeed added a Power to Hand");
        AutoTestAssert.Equal(CardType.Power, genPower.Type, "TimeOfNeed generated a Power card");
        AutoTestAssert.True(
            genPower.EnergyCost.GetWithModifiers(CostModifiers.All) == Math.Max(0, genPower.EnergyCost.Canonical - 1),
            "TimeOfNeed reduced generated Power cost by 1 this combat");

        // 16. Toasty (Flare Flick): 10 (14) dmg + Ignite active + Advance (Toasty+: ChooseMove Retract!)
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<SoulburnPower>(enemy);
        HexaCombatState.Get(combatState).ActiveSlotIndex = 0; // Searing
        int hpBeforeToasty = enemy.CurrentHp;
        var toasty = combatState.CreateCard<Toasty>(player);
        await CardPileCmd.Add(toasty, PileType.Hand);
        await PlayCardAsync(toasty, enemy);
        AutoTestAssert.Equal(hpBeforeToasty - 10, enemy.CurrentHp, "Toasty dealt 10 damage");
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<SoulburnPower>(), "Toasty ignited Searing (+6 Soulburn)");
        AutoTestAssert.Equal(1, HexaMechanics.GetActiveSlotIndex(player), "Unupgraded Toasty advanced from Slot 0 to Slot 1");

        var toastyPlus = combatState.CreateCard<Toasty>(player);
        CardCmd.Upgrade(toastyPlus);
        await CardPileCmd.Add(toastyPlus, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is HexaRetractChoice))
        {
            await PlayCardAsync(toastyPlus, enemy);
        }
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "Toasty+ Retract moved back from Slot 1 to Slot 0");
    }

    internal static async Task TestRareCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheHexaghost>("HEXA_RARE");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(e => e.IsAlive), "Alive enemy");
        var ctx = new ThrowingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1500);
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);

        async Task ClearHandAndRefillAsync()
        {
            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            pcs.Energy = 20;
        }

        // 1. EtherStep: OnPlay with 2+ cards in Hand (exercises CardSelectCmd.FromHand + SelectionScreenPrompt) & Afterlife (10 dmg)!
        await ClearHandAndRefillAsync();
        var exhFodder = combatState.CreateCard<HexaStrike>(player);
        var keepCard = combatState.CreateCard<HexaDefend>(player);
        var drawTarget = combatState.CreateCard<HexaDefend>(player);
        await CardPileCmd.Add(drawTarget, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(exhFodder, PileType.Hand);
        await CardPileCmd.Add(keepCard, PileType.Hand);
        var etherStep = combatState.CreateCard<EtherStep>(player);
        await CardPileCmd.Add(etherStep, PileType.Hand);
        int hpBeforeEs = enemy.CurrentHp;
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, exhFodder)))
        {
            await PlayCardAsync(etherStep, enemy);
        }
        AutoTestAssert.Equal(hpBeforeEs - 10, enemy.CurrentHp, "EtherStep OnPlay dealt 10 damage");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(exhFodder), "EtherStep exhausted selected card from multi-card Hand");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(keepCard), "EtherStep kept unselected card in Hand");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(drawTarget), "EtherStep drew 1 card");

        var etherStep2 = combatState.CreateCard<EtherStep>(player);
        await CardPileCmd.Add(etherStep2, PileType.Hand);
        int hpBeforeEsAfter = enemy.CurrentHp;
        await CardCmd.Exhaust(ctx, etherStep2, causedByEthereal: false);
        AutoTestAssert.Equal(hpBeforeEsAfter - 10, enemy.CurrentHp, "EtherStep Afterlife dealt 10 damage");

        // 2. ForkedFlame: Active on Slot 1 (Crushing) -> 4 dmg + Ignites previous (Slot 0 Searing -> +6 Soulburn) and next (Slot 2 Bolstering -> +1 Str, +4 Block)!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<SoulburnPower>(enemy);
        HexaCombatState.Get(combatState).ActiveSlotIndex = 1;
        int hpBeforeFork = enemy.CurrentHp;
        int strBeforeFork = player.Creature.GetPowerAmount<StrengthPower>();
        var fork = combatState.CreateCard<ForkedFlame>(player);
        await CardPileCmd.Add(fork, PileType.Hand);
        await PlayCardAsync(fork, enemy);
        AutoTestAssert.Equal(hpBeforeFork - 4, enemy.CurrentHp, "ForkedFlame dealt 4 damage");
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<SoulburnPower>(), "ForkedFlame ignited previous Slot 0 Searing (+6 Soulburn)");
        AutoTestAssert.Equal(strBeforeFork + 1, player.Creature.GetPowerAmount<StrengthPower>(), "ForkedFlame ignited next Slot 2 Bolstering (+1 Strength)");
        await PowerCmd.Remove<StrengthPower>(player.Creature);

        // 3. SearingWound: enemy loses HP equal to Soulburn WITHOUT removing Soulburn!
        await ClearHandAndRefillAsync();
        int hpBeforeSw = enemy.CurrentHp;
        var searingWound = combatState.CreateCard<SearingWound>(player);
        await CardPileCmd.Add(searingWound, PileType.Hand);
        await PlayCardAsync(searingWound);
        AutoTestAssert.Equal(hpBeforeSw - 6, enemy.CurrentHp, "SearingWound dealt 6 HP loss equal to Soulburn");
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<SoulburnPower>(), "SearingWound did not remove Soulburn");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(searingWound), "Unupgraded SearingWound exhausted");
        var searingWoundPlus = combatState.CreateCard<SearingWound>(player);
        CardCmd.Upgrade(searingWoundPlus);
        AutoTestAssert.True(!searingWoundPlus.Keywords.Contains(CardKeyword.Exhaust), "SearingWound+ removed Exhaust");

        // 4. VolcanoVisage + ParanormalForm:
        //    - VolcanoVisage applies 4 Soulburn to ALL enemies whenever a Ghostflame ignites!
        //    - ParanormalForm makes random enemy lose 6 HP whenever an Ethereal card is played!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<SoulburnPower>(enemy);
        var volcano = combatState.CreateCard<VolcanoVisage>(player);
        var para = combatState.CreateCard<ParanormalForm>(player);
        await CardPileCmd.Add(volcano, PileType.Hand);
        await CardPileCmd.Add(para, PileType.Hand);
        await PlayCardAsync(volcano);
        await PlayCardAsync(para);
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<VolcanoVisagePower>(), "VolcanoVisage applied VolcanoVisagePower(4)");
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<ParanormalFormPower>(), "ParanormalForm applied ParanormalFormPower(6)");

        await HexaMechanics.IgniteAsync(ctx, player, HexaMechanics.GetGhostflames(player)[1], extinguishFirstIfIgnited: true);
        AutoTestAssert.Equal(4, enemy.GetPowerAmount<SoulburnPower>(), "VolcanoVisage applied 4 Soulburn on Ghostflame ignition");

        int hpBeforePara = enemy.CurrentHp;
        var ethSear = combatState.CreateCard<Sear>(player); // Ethereal Attack: 5 dmg + 5 Soulburn + 6 HP loss from ParanormalForm!
        await CardPileCmd.Add(ethSear, PileType.Hand);
        await PlayCardAsync(ethSear, enemy);
        AutoTestAssert.Equal(hpBeforePara - 5 - 6, enemy.CurrentHp, "ParanormalForm dealt 6 HP loss on playing Ethereal Sear (5 + 6 = 11 total)");
        await PowerCmd.Remove<VolcanoVisagePower>(player.Creature);
        await PowerCmd.Remove<ParanormalFormPower>(player.Creature);

        // 5. LingeringShades: Retract + 12 Soulburn + returns all Ethereal cards from Discard Pile to Hand!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<SoulburnPower>(enemy);
        var discEth1 = combatState.CreateCard<Sear>(player);
        var discEth2 = combatState.CreateCard<GhostShield>(player);
        var discNonEth = combatState.CreateCard<HexaStrike>(player);
        await CardPileCmd.Add(discEth1, PileType.Discard);
        await CardPileCmd.Add(discEth2, PileType.Discard);
        await CardPileCmd.Add(discNonEth, PileType.Discard);
        int slotBeforeLs = HexaMechanics.GetActiveSlotIndex(player);
        var ls = combatState.CreateCard<LingeringShades>(player);
        await CardPileCmd.Add(ls, PileType.Hand);
        await PlayCardAsync(ls, enemy);
        AutoTestAssert.Equal((slotBeforeLs + 5) % 6, HexaMechanics.GetActiveSlotIndex(player), "LingeringShades retracted 1 slot");
        AutoTestAssert.Equal(12, enemy.GetPowerAmount<SoulburnPower>(), "LingeringShades applied 12 Soulburn");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(discEth1) && pcs.Hand.Cards.Contains(discEth2), "LingeringShades returned all Ethereal cards from Discard to Hand");
        AutoTestAssert.True(!pcs.Hand.Cards.Contains(discNonEth), "LingeringShades left non-Ethereal card in Discard");

        // 6. TimeWarp: returns from Discard to Hand on manual Advance/Retract, NOT on end-of-turn Advance!
        await ClearHandAndRefillAsync();
        var tw = combatState.CreateCard<TimeWarp>(player);
        await CardPileCmd.Add(tw, PileType.Discard);
        await HexaMechanics.AdvanceAsync(ctx, player, isEndTurn: true);
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(tw), "TimeWarp stayed in Discard on end-of-turn Advance");
        await HexaMechanics.AdvanceAsync(ctx, player, isEndTurn: false);
        AutoTestAssert.True(pcs.Hand.Cards.Contains(tw), "TimeWarp returned from Discard to Hand on manual Advance");
        await CardPileCmd.Add(tw, PileType.Discard);
        await HexaMechanics.RetractAsync(ctx, player);
        AutoTestAssert.True(pcs.Hand.Cards.Contains(tw), "TimeWarp returned from Discard to Hand on Retract");

        // 7. GhostOfSpireFuture (Into Shadow) + RetreatToShadows (Incorporeal):
        //    Retract triggers FuturePower -> PlayCardAndExhaustPower(1) -> next card costs 0 and Exhausts!
        //    RetreatToShadows retracts, loses 6 HP, grants 1 Intangible!
        await ClearHandAndRefillAsync();
        var futureCard = combatState.CreateCard<GhostOfSpireFuture>(player);
        await CardPileCmd.Add(futureCard, PileType.Hand);
        await PlayCardAsync(futureCard);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<FuturePower>(), "GhostOfSpireFuture applied FuturePower(1)");

        // Non-Exhaust Retract card (BacktrackSmack) with FuturePower(1) must land in Discard (NOT Exhaust) and leave PlayCardAndExhaustPower == 1!
        var nonExhRetract = combatState.CreateCard<BacktrackSmack>(player);
        await CardPileCmd.Add(nonExhRetract, PileType.Hand);
        await PlayCardAsync(nonExhRetract, enemy);
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(nonExhRetract), "Non-Exhaust Retract card went to Discard (not Exhaust)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<PlayCardAndExhaustPower>(), "Non-Exhaust Retract left PlayCardAndExhaustPower == 1");

        // Consume that charge cleanly before RetreatToShadows
        pcs.Energy = 0; // 0 Energy left! Playing 2-cost HeatCrush must succeed for 0 Energy and Exhaust!
        var freeCard = combatState.CreateCard<HeatCrush>(player);
        await CardPileCmd.Add(freeCard, PileType.Hand);
        AutoTestAssert.Equal(0, freeCard.EnergyCost.GetWithModifiers(CostModifiers.All), "PlayCardAndExhaustPower reduced HeatCrush cost to 0");
        await PlayCardAsync(freeCard, enemy);
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(freeCard), "PlayCardAndExhaustPower exhausted played card");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<PlayCardAndExhaustPower>(), "PlayCardAndExhaustPower decremented to 0");

        int playerHpBeforeRetreat = player.Creature.CurrentHp;
        var retreat = combatState.CreateCard<RetreatToShadows>(player);
        await CardPileCmd.Add(retreat, PileType.Hand);
        await PlayCardAsync(retreat);
        AutoTestAssert.Equal(playerHpBeforeRetreat - 6, player.Creature.CurrentHp, "RetreatToShadows dealt 6 self HP loss");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<IntangiblePower>(), "RetreatToShadows granted 1 Intangible");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<PlayCardAndExhaustPower>(), "Retreat triggered FuturePower -> PlayCardAndExhaustPower(1)");
        await PowerCmd.Remove<PlayCardAndExhaustPower>(player.Creature);
        await PowerCmd.Remove<FuturePower>(player.Creature);
        await PowerCmd.Remove<IntangiblePower>(player.Creature);

        // 8. GhostflameInferno (Instant Inferno): 12 Soulburn + switches to Slot 5 and ignites Inferno!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<SoulburnPower>(enemy);
        var flames = HexaMechanics.GetGhostflames(player);
        for (int i = 0; i < 6; i++)
            await HexaMechanics.ExtinguishAsync(ctx, player, flames[i]);
        await HexaMechanics.IgniteAsync(ctx, player, flames[0]); // 1 ignited -> Inferno hits twice (2 * 4 = 8 dmg)!
        await PowerCmd.Remove<SoulburnPower>(enemy);
        int hpBeforeGi = enemy.CurrentHp;
        var gi = combatState.CreateCard<GhostflameInferno>(player);
        await CardPileCmd.Add(gi, PileType.Hand);
        await PlayCardAsync(gi, enemy);
        AutoTestAssert.Equal(12, enemy.GetPowerAmount<SoulburnPower>(), "GhostflameInferno applied 12 Soulburn");
        AutoTestAssert.Equal(5, HexaMechanics.GetActiveSlotIndex(player), "GhostflameInferno switched active slot to 5 (Inferno)");
        AutoTestAssert.True(flames[5].IsIgnited, "GhostflameInferno ignited Slot 5 Inferno");
        AutoTestAssert.Equal(hpBeforeGi - 8, enemy.CurrentHp, "GhostflameInferno triggered Inferno for 4 * 2 = 8 damage");

        // 9. ApocalypticArmor (threshold 6) + ApocalypticArmor+ threshold lowering (6 -> 5, NOT 11!) + unupgraded threshold 6 triggering on Slot 5 Inferno via UnlimitedPower!
        await ClearHandAndRefillAsync();
        await PowerCmd.Remove<EnhancePower>(player.Creature);
        var doomsdayCheck = combatState.CreateCard<ApocalypticArmor>(player);
        var doomsdayPlusCheck = combatState.CreateCard<ApocalypticArmor>(player);
        CardCmd.Upgrade(doomsdayPlusCheck); // threshold 5
        await CardPileCmd.Add(doomsdayCheck, PileType.Hand);
        await CardPileCmd.Add(doomsdayPlusCheck, PileType.Hand);
        await PlayCardAsync(doomsdayCheck);
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<ApocalypticArmorPower>(), "Unupgraded ApocalypticArmor applied threshold 6");
        await PlayCardAsync(doomsdayPlusCheck);
        AutoTestAssert.Equal(5, player.Creature.GetPowerAmount<ApocalypticArmorPower>(), "ApocalypticArmor+ lowered existing threshold 6 -> 5 (did not stack to 11)");
        await PowerCmd.Remove<ApocalypticArmorPower>(player.Creature);

        // Now test UNUPGRADED threshold 6 triggering on Slot 5 Inferno via UnlimitedPower!
        var doomsday6 = combatState.CreateCard<ApocalypticArmor>(player);
        await CardPileCmd.Add(doomsday6, PileType.Hand);
        await PlayCardAsync(doomsday6);
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<ApocalypticArmorPower>(), "ApocalypticArmor applied threshold 6");

        var unlim = combatState.CreateCard<UnlimitedPower>(player);
        CardCmd.Upgrade(unlim); // cost 3
        await CardPileCmd.Add(unlim, PileType.Hand);
        await PlayCardAsync(unlim);
        AutoTestAssert.Equal(5, HexaMechanics.GetActiveSlotIndex(player), "UnlimitedPower ended on Slot 5");
        AutoTestAssert.Equal(2, HexaMechanics.GetIntensity(player), "UnlimitedPower 6/6 Inferno granted +2 Intensity");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ApocalypticArmorPower>(), "Unupgraded ApocalypticArmorPower(6) consumed on Slot 5 6/6 Inferno ignition");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<HexaTimeStopPower>(), "Unupgraded ApocalypticArmorPower(6) granted HexaTimeStopPower(1)");
        await PowerCmd.Remove<StrengthPower>(player.Creature);
        await PowerCmd.Remove<EnhancePower>(player.Creature);

        // 10. TurnItUp (+3 temp Intensity/Str/Dex), HexaLevel+ (+2 immediate +2/turn Intensity), GiftsFromTheDead (+1 Afterlife/turn), GhostOfSpirePresent (+1 Energy/turn + Extinguish instead of Advance)!
        await ClearHandAndRefillAsync();
        var turnItUp = combatState.CreateCard<TurnItUp>(player);
        CardCmd.Upgrade(turnItUp);
        AutoTestAssert.True(turnItUp.Keywords.Contains(CardKeyword.Retain), "TurnItUp+ has Retain");
        await CardPileCmd.Add(turnItUp, PileType.Hand);
        await PlayCardAsync(turnItUp);
        AutoTestAssert.Equal(3, HexaMechanics.GetIntensity(player), "TurnItUp granted +3 Intensity");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<LoseIntensityPower>(), "TurnItUp applied LoseIntensityPower(3)");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<StrengthPower>(), "TurnItUp granted +3 temp Strength");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<DexterityPower>(), "TurnItUp granted +3 temp Dexterity");

        var hexaLevel = combatState.CreateCard<HexaLevel>(player);
        CardCmd.Upgrade(hexaLevel); // +2 immediate Intensity + HexalevelPower(2)
        var gifts = combatState.CreateCard<GiftsFromTheDead>(player);
        var present = combatState.CreateCard<GhostOfSpirePresent>(player);
        await CardPileCmd.Add(hexaLevel, PileType.Hand);
        await CardPileCmd.Add(gifts, PileType.Hand);
        await CardPileCmd.Add(present, PileType.Hand);
        await PlayCardAsync(hexaLevel);
        await PlayCardAsync(gifts);
        await PlayCardAsync(present);
        AutoTestAssert.Equal(5, HexaMechanics.GetIntensity(player), "HexaLevel+ immediately granted +2 Intensity (3 -> 5)");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<HexalevelPower>(), "HexaLevel+ applied HexalevelPower(2)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<GiftsFromTheDeadPower>(), "GiftsFromTheDead applied GiftsFromTheDeadPower(1)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<HexaEnergyPerTurnPower>(), "GhostOfSpirePresent applied HexaEnergyPerTurnPower(1)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StopFromAdvancingPower>(), "GhostOfSpirePresent applied StopFromAdvancingPower");

        // Ignite Slot 0 and leave it active so we can verify StopFromAdvancingPower extinguishes Slot 0 instead of advancing!
        HexaCombatState.Get(combatState).ActiveSlotIndex = 0;
        await HexaMechanics.IgniteAsync(ctx, player, flames[0], extinguishFirstIfIgnited: true);
        int exhaustedThisTurn = HexaCombatState.Get(combatState).CardsExhaustedThisTurn;
        AutoTestAssert.True(exhaustedThisTurn >= 2, "Multiple cards exhausted during Round 1");

        // End turn -> HexaTimeStopPower triggers an EXTRA PLAYER TURN (enemy skipped)!
        int turnBeforeEnd = pcs.TurnNumber;
        var extraTurnTcs = new TaskCompletionSource();
        void OnExtraTurnStarted(CombatState state)
        {
            if (state.CurrentSide == CombatSide.Player && pcs.TurnNumber > turnBeforeEnd)
                extraTurnTcs.TrySetResult();
        }
        CombatManager.Instance.TurnStarted += OnExtraTurnStarted;
        try
        {
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
                new MegaCrit.Sts2.Core.GameActions.EndPlayerTurnAction(player, combatState.RoundNumber));
            await extraTurnTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        finally
        {
            CombatManager.Instance.TurnStarted -= OnExtraTurnStarted;
        }

        AutoTestAssert.Equal(turnBeforeEnd + 1, pcs.TurnNumber, "HexaTimeStopPower started extra player turn");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<HexaTimeStopPower>(), "HexaTimeStopPower decremented to 0 after extra turn");
        AutoTestAssert.Equal(0, HexaMechanics.GetActiveSlotIndex(player), "StopFromAdvancingPower prevented end-of-turn Advance (stayed on Slot 0)");
        AutoTestAssert.True(!flames[0].IsIgnited, "StopFromAdvancingPower extinguished active Slot 0 at end of turn");
        // Intensity: started 5 -> LoseIntensityPower(-3) -> 2 -> HexalevelPower(+2) at turn start -> 4!
        AutoTestAssert.Equal(4, HexaMechanics.GetIntensity(player), "LoseIntensityPower(-3) + HexalevelPower(+2) resulted in Intensity == 4");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<LoseIntensityPower>(), "LoseIntensityPower removed at end of turn");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<StrengthPower>(), "TurnItUp temporary Strength expired at end of turn");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DexterityPower>(), "TurnItUp temporary Dexterity expired at end of turn");
        AutoTestAssert.True(pcs.Energy >= 4, $"HexaEnergyPerTurnPower granted +1 Energy at turn start (Energy={pcs.Energy})");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is HexaCard { HasAfterlife: true }), "GiftsFromTheDeadPower added an Afterlife card to Hand at turn start");

        // 11. UnleashSpirits: deals 10 damage (1 + CardsExhaustedLastTurn) times!
        int expectedHits = 1 + HexaCombatState.Get(combatState).CardsExhaustedLastTurn;
        AutoTestAssert.True(expectedHits >= 3, $"CardsExhaustedLastTurn populated ({expectedHits} hits)");
        if (enemy.Block > 0)
            await CreatureCmd.LoseBlock(ctx, enemy, enemy.Block, null);
        int hpBeforeUnleash = enemy.CurrentHp;
        pcs.Energy = 10;
        var unleash = combatState.CreateCard<UnleashSpirits>(player);
        await CardPileCmd.Add(unleash, PileType.Hand);
        await PlayCardAsync(unleash);
        AutoTestAssert.Equal(hpBeforeUnleash - 10 * expectedHits, enemy.CurrentHp, $"UnleashSpirits dealt 10 * {expectedHits} = {10 * expectedHits} damage");
    }
}
