using Downfall.Collector;
using Downfall.Collector.Cards;
using Downfall.Collector.Cards.Collectibles;
using Downfall.Collector.Powers;
using Downfall.Collector.Relics;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Testing;

internal static class CollectorTests
{
    public static async Task TestVisualsRuntime()
    {
        await DownfallAutoTestRunner.RunCharacterVisualsRuntimeTest<TheCollector>(
            "COLLECTOR_VIS",
            path => path == "res://Downfall/images/collector/char/collector.png");
        DownfallAutoTestRunner.CleanupRun();

        var (player, _, _) = await DownfallAutoTestRunner.StartCombatAsync<TheCollector>("COLLECTOR_VIS_CHECK");
        AutoTestAssert.True(player.Character is TheCollector, "Character is TheCollector");
        AutoTestAssert.Equal(65, player.Creature.MaxHp, "TheCollector starts with 65 Max HP");
        AutoTestAssert.Equal(10, player.Deck.Cards.Count, "TheCollector starting deck has 10 cards");
        AutoTestAssert.True(player.Relics.Any(r => r is EmeraldTorch), "TheCollector starts with EmeraldTorch");

        var starter = player.Relics.OfType<EmeraldTorch>().First();
        AutoTestAssert.True(starter.GetUpgradeReplacement() is PrismaticTorch, "EmeraldTorch Touch of Orobas upgrade is PrismaticTorch");

        var seaGlassLoc = new LocString("relics", "SEA_GLASS.DOWNFALL-THE_COLLECTOR.title");
        AutoTestAssert.True(seaGlassLoc.Exists(), "Sea Glass title loc exists for TheCollector");
        AutoTestAssert.Equal("Sea Glass (Collector)", seaGlassLoc.GetFormattedText(), "Sea Glass title text matches");

        var bgScene = ResourceLoader.Load<PackedScene>("res://Downfall/scenes/collector/char_select_bg.tscn");
        AutoTestAssert.NotNull(bgScene, "Collector char_select_bg.tscn loads cleanly");
        AutoTestAssert.True(ResourceLoader.Exists("res://images/ui/rest_site/option_downfall_release.png"), "Collector Release rest site icon exists");
    }

    public static async Task TestStarterMechanicsAndCollection()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheCollector>("COLLECTOR_10A");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy");
        var ctx = new BlockingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000m);

        // 1. EmeraldTorch Turn 1 start: 4 Energy (3 base + 1 Reserve — no double-grant!)
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ReservesPower>(), "EmeraldTorch granted 1 Reserve on Turn 1");
        AutoTestAssert.Equal(4, pcs.Energy, "Turn 1 starts at 4 Energy (3 base + 1 Reserve, no double-grant)");

        // Spend 3 Energy (playing 3x 1-cost Strikes) -> 1 Reserve persists to Turn 2!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        for (int i = 0; i < 3; i++)
        {
            var s = combatState.CreateCard<CollectorStrike>(player);
            await CardPileCmd.Add(s, PileType.Hand);
            AutoTestAssert.True(s.TryManualPlay(enemy), $"Play Strike #{i + 1}");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(1, pcs.Energy, "After spending 3 of 4 Energy, 1 Energy remains");
        AutoTestAssert.Equal(1, CollectorMechanics.GetReserves(player), "1 Reserve remains untouched after spending 3 regular Energy");

        // End turn -> Turn 2 should restore 3 regular + 1 persisted Reserve = 4 Energy!
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ReservesPower>(), "1 Reserve persisted into Turn 2");
        AutoTestAssert.Equal(4, pcs.Energy, "Turn 2 starts at 3 + 1 = 4 Energy");

        // Spend all 4 Energy (regular 3 + 1 Reserve) -> 0 Reserves remain!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        for (int i = 0; i < 4; i++)
        {
            var d = combatState.CreateCard<CollectorDefend>(player);
            await CardPileCmd.Add(d, PileType.Hand);
            AutoTestAssert.True(d.TryManualPlay(null), $"Play Defend #{i + 1}");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(0, pcs.Energy, "Spent all 4 Energy");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<ReservesPower>(), "Spending into Reserve consumed ReservesPower to 0");

        // 2. YouAreMine (1 Weak, 1 Vulnerable -> Afflicted + 6 Doom) & DoomPower + DemisePower!
        pcs.Energy = 10;
        var yam = combatState.CreateCard<YouAreMine>(player);
        await CardPileCmd.Add(yam, PileType.Hand);
        await CardCmd.AutoPlay(ctx, yam, enemy, AutoPlayType.Default);
        AutoTestAssert.True(CollectorMechanics.IsAfflicted(enemy), "YouAreMine made enemy Afflicted (Weak + Vulnerable)");
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<CollectorDoomPower>(), "YouAreMine applied 6 Doom");

        int enemyHpBeforeDoom1 = enemy.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(enemyHpBeforeDoom1 - 6, enemy.CurrentHp, "Doom dealt 6 HP loss at start of enemy turn");
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<CollectorDoomPower>(), "Doom stayed on Afflicted enemy");

        // Remove Weak so enemy is no longer Afflicted, apply CollectorDemisePower(1) -> Doom deals 6 and STAYS while Demise decrements 1 -> 0!
        await PowerCmd.Remove<WeakPower>(enemy);
        AutoTestAssert.True(!CollectorMechanics.IsAfflicted(enemy), "Enemy is no longer Afflicted without Weak");
        await PowerCmd.Apply<CollectorDemisePower>(ctx, enemy, 1m, player.Creature, null);
        int enemyHpBeforeDoom2 = enemy.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(enemyHpBeforeDoom2 - 6, enemy.CurrentHp, "Doom dealt 6 HP loss with Demise");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<CollectorDemisePower>(), "CollectorDemisePower decremented 1 -> 0");
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<CollectorDoomPower>(), "CollectorDemisePower preserved CollectorDoomPower");

        // Next turn without Afflicted or Demise -> Doom deals 6 and REMOVES itself!
        int enemyHpBeforeDoom3 = enemy.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(enemyHpBeforeDoom3 - 6, enemy.CurrentHp, "Doom dealt 6 HP loss on non-Afflicted enemy");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<CollectorDoomPower>(), "CollectorDoomPower cleared on non-Afflicted enemy without Demise");

        // 3. FuelTheFire (Pyre) + Ember (Unplayable, Retain, Exhaust -> +1 Strength) + NextTurnReservePower!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        pcs.Energy = 10;
        var ftf = combatState.CreateCard<FuelTheFire>(player);
        await CardPileCmd.Add(ftf, PileType.Hand);
        AutoTestAssert.True(!ftf.CanPlay(), "Pyre card FuelTheFire is unplayable with no other cards in Hand");

        var ember = combatState.CreateCard<Ember>(player);
        await CardPileCmd.Add(ember, PileType.Hand);
        AutoTestAssert.True(!ember.CanPlay(), "Ember is Unplayable");
        AutoTestAssert.True(ftf.CanPlay(), "FuelTheFire is playable once Ember is in Hand");
        await CardCmd.AutoPlay(ctx, ftf, null, AutoPlayType.Default);
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(ember), "FuelTheFire Pyred (Exhausted) Ember");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "Exhausting Ember granted +1 Strength");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<NextTurnReservePower>(), "FuelTheFire applied NextTurnReservePower(2)");

        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<ReservesPower>(), "NextTurnReservePower granted 2 Reserves on next turn");
        AutoTestAssert.Equal(5, pcs.Energy, "3 base + 2 Reserves = 5 Energy on next turn");

        // 4. CollectibleCardReward (Essence < 3 returns false; Essence >= 3 claims & deducts; ToSerializable round-trips!),
        //    SavedProperty round-trip, Touch of Orobas (EmeraldTorch -> PrismaticTorch preserves state!),
        //    StashAwayRestSiteOption (Release shifts BottledCollectionIndex + grants 40 Gold!), and Collection draw + NoDraw guard!
        var runData = CollectorRunState.GetOrCreate(player);
        AutoTestAssert.Equal(5, runData.Essence, "Collector starts with 5 Essence");

        var rewardCard1 = combatState.CreateCard<JawWormCard>(player);
        var reward1 = new CollectibleCardReward(rewardCard1, player);
        var roundTripReward = Reward.FromSerializable(reward1.ToSerializable(), player);
        AutoTestAssert.NotNull(roundTripReward, "CollectibleCardReward ToSerializable round-trips cleanly via Reward.FromSerializable");

        AutoTestAssert.True(await reward1.SelectUnsynchronized(), "Claiming CollectibleCardReward succeeds with 5 Essence");
        AutoTestAssert.Equal(2, runData.Essence, "Claiming Collectible deducted 3 Essence (5 -> 2)");
        AutoTestAssert.Equal(1, runData.Collection.Count, "Collection now has 1 card (JawWormCard)");

        // Insufficient Essence (< 3) returns false without mutating!
        var rewardCard2 = combatState.CreateCard<ByrdCard>(player);
        var reward2 = new CollectibleCardReward(rewardCard2, player);
        AutoTestAssert.True(!await reward2.SelectUnsynchronized(), "CollectibleCardReward returns false when Essence < 3");
        AutoTestAssert.Equal(2, runData.Essence, "Essence unchanged after rejected reward");
        AutoTestAssert.Equal(1, runData.Collection.Count, "Collection unchanged after rejected reward");

        // Add 2nd Collectible, bottle index 1, verify [SavedProperty] round-trip & Touch of Orobas upgrade!
        runData.ChangeEssence(player, 4); // 2 -> 6
        AutoTestAssert.True(await reward2.SelectUnsynchronized(), "Claimed 2nd Collectible (ByrdCard) with 6 Essence");
        AutoTestAssert.True(runData.UpgradeAndBottleAt(player, 1), "Upgraded & bottled Collection index 1 (ByrdCard)");
        AutoTestAssert.Equal(1, runData.BottledCollectionIndex, "BottledCollectionIndex is 1");

        var emeraldTorch = player.Relics.OfType<EmeraldTorch>().First();
        var torchRoundTrip = (EmeraldTorch)RelicModel.FromSerializable(emeraldTorch.ToSerializable());
        AutoTestAssert.Equal(3, torchRoundTrip.SavedEssence, "EmeraldTorch [SavedProperty] SavedEssence round-trips");
        AutoTestAssert.Equal(1, torchRoundTrip.SavedBottledIndex, "EmeraldTorch [SavedProperty] SavedBottledIndex round-trips");
        AutoTestAssert.True(torchRoundTrip.SavedCollectionCsv.Contains("JAW_WORM_CARD"), "EmeraldTorch SavedCollectionCsv round-trips");

        // Replace EmeraldTorch -> PrismaticTorch (Touch of Orobas) and confirm zero state loss!
        await RelicCmd.Replace(emeraldTorch, ModelDb.Relic<PrismaticTorch>().ToMutable());
        var prism = AutoTestAssert.NotNull(player.Relics.OfType<PrismaticTorch>().FirstOrDefault(), "PrismaticTorch obtained via Replace");
        AutoTestAssert.Equal(3, prism.SavedEssence, "PrismaticTorch preserved Essence=3 across Orobas swap");
        AutoTestAssert.Equal(1, prism.SavedBottledIndex, "PrismaticTorch preserved BottledCollectionIndex=1 across Orobas swap");

        // Test PrismaticTorch Ember exhaust -> +1 Reserve once per turn!
        int reservesBeforeEmber = CollectorMechanics.GetReserves(player);
        var prismEmber1 = combatState.CreateCard<Ember>(player);
        await CardPileCmd.Add(prismEmber1, PileType.Hand);
        await CardCmd.Exhaust(ctx, prismEmber1);
        AutoTestAssert.Equal(reservesBeforeEmber + 1, CollectorMechanics.GetReserves(player), "PrismaticTorch granted +1 Reserve on first Ember exhaust");
        var prismEmber2 = combatState.CreateCard<Ember>(player);
        await CardPileCmd.Add(prismEmber2, PileType.Hand);
        await CardCmd.Exhaust(ctx, prismEmber2);
        AutoTestAssert.Equal(reservesBeforeEmber + 1, CollectorMechanics.GetReserves(player), "PrismaticTorch triggered only once per turn");

        // Test CombatCollection init (bottled ByrdCard+ on top!), NoDraw guard, and real draw!
        CollectorMechanics.InitializeCombatCollection(combatState, player);
        var cData = CollectorCombatState.Get(combatState).GetPlayer(player);
        AutoTestAssert.Equal(2, cData.CombatCollection.Count, "CombatCollection initialized with 2 cards");
        AutoTestAssert.True(cData.CombatCollection[0] is ByrdCard { IsUpgraded: true }, "Bottled ByrdCard+ is on top of CombatCollection");

        // NoDrawPower blocks DrawFromCollectionAsync without stranding ByrdCard+ in Draw Pile!
        await PowerCmd.Apply<NoDrawPower>(ctx, player.Creature, 1m, player.Creature, null);
        var blockedDraw = await CollectorMechanics.DrawFromCollectionAsync(ctx, player);
        AutoTestAssert.True(blockedDraw is null, "NoDrawPower prevented DrawFromCollectionAsync");
        AutoTestAssert.Equal(2, cData.CombatCollection.Count, "Blocked draw did NOT remove card from CombatCollection");
        await PowerCmd.Remove<NoDrawPower>(player.Creature);

        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var drawnCollectible = await CollectorMechanics.DrawFromCollectionAsync(ctx, player);
        AutoTestAssert.True(drawnCollectible is ByrdCard { IsUpgraded: true }, "DrawFromCollectionAsync returned bottled ByrdCard+");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(drawnCollectible!), "ByrdCard+ drawn into Hand");
        AutoTestAssert.True(drawnCollectible!.Keywords.Contains(CardKeyword.Exhaust), "CollectibleCard has Exhaust keyword");

        // Test StashAwayRestSiteOption (Release index 0 -> shifts BottledCollectionIndex 1 -> 0 + grants 40 Gold!)
        int goldBeforeRelease = player.Gold;
        var releaseOpt = new StashAwayRestSiteOption(player);
        AutoTestAssert.True(releaseOpt.IsEnabled, "StashAwayRestSiteOption is enabled with cards in Collection");
        using (DownfallAutoTestRunner.PreferCards(c => c is JawWormCard))
        {
            AutoTestAssert.True(await releaseOpt.OnSelect(), "Release rest site option succeeded");
        }
        AutoTestAssert.Equal(goldBeforeRelease + StashAwayRestSiteOption.GoldGranted, player.Gold, "Release granted 40 Gold");
        AutoTestAssert.Equal(1, runData.Collection.Count, "Collection reduced to 1 card");
        AutoTestAssert.Equal(0, runData.BottledCollectionIndex, "BottledCollectionIndex shifted cleanly from 1 -> 0");

        // 5. Verify EVERY Collectible pool type resolves cleanly (catches abstract/shadowed type bugs!),
        //    plus CreateCollectibleForEnemy(Monster/Elite/Boss) & CreateRandomCollectible!
        foreach (var colType in CollectorMechanics.AllPoolCollectibleTypes)
        {
            AutoTestAssert.True(!colType.IsAbstract && typeof(CollectibleCard).IsAssignableFrom(colType),
                $"Collectible pool type {colType.Name} is a concrete CollectibleCard");
            var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(colType));
            AutoTestAssert.True(canonical is CollectibleCard, $"ModelDb resolved {colType.Name} as CollectibleCard");
            if (canonical.Type == CardType.Power)
                AutoTestAssert.True(!canonical.CanonicalKeywords.Contains(CardKeyword.Exhaust),
                    $"Power Collectible {colType.Name} does not carry misleading Exhaust keyword");
            else
                AutoTestAssert.True(canonical.CanonicalKeywords.Contains(CardKeyword.Exhaust),
                    $"Non-Power Collectible {colType.Name} carries Exhaust keyword");
        }
        AutoTestAssert.True(CollectorMechanics.CreateCollectibleForEnemy(player, enemy, MegaCrit.Sts2.Core.Rooms.RoomType.Monster) is CollectibleCard,
            "CreateCollectibleForEnemy(Monster) returns CollectibleCard");
        AutoTestAssert.True(CollectorMechanics.CreateCollectibleForEnemy(player, enemy, MegaCrit.Sts2.Core.Rooms.RoomType.Elite) is CollectibleCard,
            "CreateCollectibleForEnemy(Elite) returns CollectibleCard");
        AutoTestAssert.True(CollectorMechanics.CreateCollectibleForEnemy(player, enemy, MegaCrit.Sts2.Core.Rooms.RoomType.Boss) is CollectibleCard,
            "CreateCollectibleForEnemy(Boss) returns CollectibleCard");
        for (int i = 0; i < 8; i++)
        {
            AutoTestAssert.True(CollectorMechanics.CreateRandomCollectible(combatState, player) is CollectibleCard,
                $"CreateRandomCollectible #{i + 1} returns CollectibleCard");
        }

        // 6. Verify LastTurnUnusedBlock records expired Block AND resets to 0 on a subsequent 0-Block turn!
        await CreatureCmd.GainBlock(player.Creature, 50m, ValueProp.Move, null);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.True(cData.LastTurnUnusedBlock > 0, $"LastTurnUnusedBlock recorded leftover Block ({cData.LastTurnUnusedBlock})");

        // Next turn with 0 Block -> LastTurnUnusedBlock must reset to 0 (no stale value)!
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(0, cData.LastTurnUnusedBlock, "LastTurnUnusedBlock reset to 0 on a 0-Block turn");

        // 7. Play representative Collectibles in combat: CultistCard (StrengthOverTurnsPower), SentryCard, BronzeOrbCard, OuroborosCard!
        pcs.Energy = 10;
        var cultistCard = combatState.CreateCard<CultistCard>(player);
        cultistCard.UpgradeInternal(); // 1 -> 0 cost, 3 turns of +1 Strength
        await CardPileCmd.Add(cultistCard, PileType.Hand);
        await CardCmd.AutoPlay(ctx, cultistCard, null, AutoPlayType.Default);
        var empower = AutoTestAssert.NotNull(player.Creature.GetPower<StrengthOverTurnsPower>(), "CultistCard+ applied StrengthOverTurnsPower");
        AutoTestAssert.Equal(3, empower.Amount, "CultistCard+ applied 3 turns of StrengthOverTurnsPower");
        int strBeforeEmpower = player.Creature.GetPowerAmount<StrengthPower>();
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(strBeforeEmpower + 1, player.Creature.GetPowerAmount<StrengthPower>(), "StrengthOverTurnsPower granted +1 Strength on next turn");

        pcs.Energy = 10;
        int enemyHpBeforeSentry = enemy.CurrentHp;
        var sentryCard = combatState.CreateCard<SentryCard>(player);
        await CardPileCmd.Add(sentryCard, PileType.Hand);
        await CardCmd.AutoPlay(ctx, sentryCard, enemy, AutoPlayType.Default);
        AutoTestAssert.True(enemy.CurrentHp < enemyHpBeforeSentry, "SentryCard dealt damage to enemy");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(sentryCard), "SentryCard Exhausted after play");

        var orbCard = combatState.CreateCard<BronzeOrbCard>(player);
        await CardPileCmd.Add(orbCard, PileType.Hand);
        await CardCmd.AutoPlay(ctx, orbCard, null, AutoPlayType.Default);
        AutoTestAssert.True(player.Creature.Block > 0, "BronzeOrbCard granted Block");

        var ouroEmber = combatState.CreateCard<Ember>(player);
        var ouro = combatState.CreateCard<OuroborosCard>(player);
        await CardPileCmd.Add(ouroEmber, PileType.Hand);
        await CardPileCmd.Add(ouro, PileType.Hand);
        await CardCmd.AutoPlay(ctx, ouro, enemy, AutoPlayType.Default);
        AutoTestAssert.True(CollectorMechanics.IsAfflicted(enemy), "OuroborosCard applied Weak + Vulnerable (Afflicted)");
        AutoTestAssert.True(player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>() >= 3, "OuroborosCard granted 3 Temp HP");
    }
}
