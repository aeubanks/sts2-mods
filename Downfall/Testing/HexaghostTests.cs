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
}
