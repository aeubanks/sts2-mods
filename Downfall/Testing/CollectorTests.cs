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

    public static async Task TestCommonCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheCollector>("COLLECTOR_10B");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy");
        var ctx = new BlockingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000m);

        static async Task ClearHandAsync(PlayerCombatState p)
        {
            foreach (var c in p.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
        }

        // 1. Arrogance -> Block + NextAttackAppliesDoomPower -> next Attack applies Doom & clears power!
        await ClearHandAsync(pcs);
        pcs.Energy = 10;
        var arrogance = combatState.CreateCard<Arrogance>(player);
        await CardPileCmd.Add(arrogance, PileType.Hand);
        await CardCmd.AutoPlay(ctx, arrogance, null, AutoPlayType.Default);
        AutoTestAssert.Equal(7, player.Creature.Block, "Arrogance granted 7 Block");
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<NextAttackAppliesDoomPower>(), "Arrogance applied NextAttackAppliesDoomPower(4)");

        var ashen = combatState.CreateCard<AshenStrike>(player);
        await CardPileCmd.Add(ashen, PileType.Hand);
        await CardCmd.AutoPlay(ctx, ashen, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<NextAttackAppliesDoomPower>(), "NextAttackAppliesDoomPower consumed on Attack");
        AutoTestAssert.Equal(4, enemy.GetPowerAmount<CollectorDoomPower>(), "NextAttackAppliesDoomPower applied 4 Doom on Attack");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is Ember), "AshenStrike added an Ember to Hand");

        // 2. BramblesparKindling+ (Unplayable, Exhaust -> BurningStrike+) & IronbarkKindling+ (Unplayable, Retain, Exhaust -> 9 Block)
        var bramble = combatState.CreateCard<BramblesparKindling>(player);
        bramble.UpgradeInternal();
        AutoTestAssert.True(!bramble.CanPlay(), "BramblesparKindling is Unplayable");
        await CardPileCmd.Add(bramble, PileType.Hand);
        await CardCmd.Exhaust(ctx, bramble);
        var burningStrike = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<BurningStrike>().FirstOrDefault(), "BramblesparKindling+ Exhaust added BurningStrike+ to Hand");
        AutoTestAssert.True(burningStrike.IsUpgraded, "BurningStrike from BramblesparKindling+ is upgraded");
        int hpBeforeBurn = enemy.CurrentHp;
        await CardCmd.AutoPlay(ctx, burningStrike, enemy, AutoPlayType.Default);
        AutoTestAssert.True(enemy.CurrentHp < hpBeforeBurn, "BurningStrike+ dealt damage");

        var ironbark = combatState.CreateCard<IronbarkKindling>(player);
        ironbark.UpgradeInternal();
        AutoTestAssert.True(!ironbark.CanPlay(), "IronbarkKindling is Unplayable");
        AutoTestAssert.True(ironbark.Keywords.Contains(CardKeyword.Retain), "IronbarkKindling+ has Retain");
        int blockBeforeIron = player.Creature.Block;
        await CardPileCmd.Add(ironbark, PileType.Hand);
        await CardCmd.Exhaust(ctx, ironbark);
        AutoTestAssert.Equal(blockBeforeIron + 9, player.Creature.Block, "IronbarkKindling Exhaust granted 9 Block");

        // 3. CollectorCondemn (Vulnerable + Doom) & CollectorSuckerPunch (dmg + Weak) & ItMattersNot (Block + +1 Weak/Vuln)
        await PowerCmd.Remove<CollectorDoomPower>(enemy);
        var condemn = combatState.CreateCard<CollectorCondemn>(player);
        await CardPileCmd.Add(condemn, PileType.Hand);
        await CardCmd.AutoPlay(ctx, condemn, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "Condemn applied 1 Vulnerable");
        AutoTestAssert.Equal(5, enemy.GetPowerAmount<CollectorDoomPower>(), "Condemn applied 5 Doom");

        var sucker = combatState.CreateCard<CollectorSuckerPunch>(player);
        await CardPileCmd.Add(sucker, PileType.Hand);
        await CardCmd.AutoPlay(ctx, sucker, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "CollectorSuckerPunch applied 1 Weak");

        int blockBeforeImn = player.Creature.Block;
        var imn = combatState.CreateCard<ItMattersNot>(player);
        await CardPileCmd.Add(imn, PileType.Hand);
        await CardCmd.AutoPlay(ctx, imn, null, AutoPlayType.Default);
        AutoTestAssert.Equal(blockBeforeImn + 13, player.Creature.Block, "ItMattersNot granted 13 Block");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "ItMattersNot increased Weak 1 -> 2");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "ItMattersNot increased Vulnerable 1 -> 2");

        // 4. InevitableDemise -> damage + CollectorDemisePower(1)
        var demiseCard = combatState.CreateCard<InevitableDemise>(player);
        await CardPileCmd.Add(demiseCard, PileType.Hand);
        await CardCmd.AutoPlay(ctx, demiseCard, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<CollectorDemisePower>(), "InevitableDemise applied CollectorDemisePower(1)");

        // 5. Invigorate+ ("Flash") -> Pyre + Choose Trip+ or Blind+!
        await ClearHandAsync(pcs);
        var invFodder = combatState.CreateCard<CollectorDefend>(player);
        var inv = combatState.CreateCard<Invigorate>(player);
        inv.UpgradeInternal();
        await CardPileCmd.Add(invFodder, PileType.Hand);
        await CardPileCmd.Add(inv, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => c is CollectorTrip))
        {
            await CardCmd.AutoPlay(ctx, inv, null, AutoPlayType.Default);
        }
        var trip = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<CollectorTrip>().FirstOrDefault(), "Invigorate+ added CollectorTrip+ to Hand");
        AutoTestAssert.True(trip.IsUpgraded, "CollectorTrip from Invigorate+ is upgraded");
        AutoTestAssert.Equal(TargetType.AllEnemies, trip.TargetType, "CollectorTrip+ targets AllEnemies");
        int vulnBeforeTrip = enemy.GetPowerAmount<VulnerablePower>();
        await CardCmd.AutoPlay(ctx, trip, null, AutoPlayType.Default);
        AutoTestAssert.Equal(vulnBeforeTrip + 2, enemy.GetPowerAmount<VulnerablePower>(), "CollectorTrip+ applied 2 Vulnerable to all enemies");

        var blind = combatState.CreateCard<CollectorBlind>(player);
        int weakBeforeBlind = enemy.GetPowerAmount<WeakPower>();
        await CardPileCmd.Add(blind, PileType.Hand);
        await CardCmd.AutoPlay(ctx, blind, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(weakBeforeBlind + 2, enemy.GetPowerAmount<WeakPower>(), "CollectorBlind applied 2 Weak");

        // 6. JadedJabs (Pyre 2-cost YouAreMine -> 2 + 1 = 3 Shivs!)
        await ClearHandAsync(pcs);
        var jabFodder = combatState.CreateCard<YouAreMine>(player); // cost 2
        var jabs = combatState.CreateCard<JadedJabs>(player);
        await CardPileCmd.Add(jabFodder, PileType.Hand);
        await CardPileCmd.Add(jabs, PileType.Hand);
        await CardCmd.AutoPlay(ctx, jabs, enemy, AutoPlayType.Default);
        int shivCount = pcs.Hand.Cards.Count(c => c is MegaCrit.Sts2.Core.Models.Cards.Shiv);
        AutoTestAssert.Equal(3, shivCount, "JadedJabs Pyring 2-cost card added 2 + 1 = 3 Shivs");

        // 7. MysteryWeaving (9 Block - 2 * other cards in Hand!)
        await ClearHandAsync(pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var mw1 = combatState.CreateCard<CollectorStrike>(player);
        var mw2 = combatState.CreateCard<CollectorStrike>(player);
        var mystery = combatState.CreateCard<MysteryWeaving>(player);
        await CardPileCmd.Add(mw1, PileType.Hand);
        await CardPileCmd.Add(mw2, PileType.Hand);
        await CardPileCmd.Add(mystery, PileType.Hand);
        await CardCmd.AutoPlay(ctx, mystery, null, AutoPlayType.Default);
        AutoTestAssert.Equal(5, player.Creature.Block, "MysteryWeaving with 2 other cards in Hand granted 9 - 4 = 5 Block");

        // 8. Roast (0 cost Pyre 4 dmg) & SpiritLeech (13 dmg + 11 Block when Afflicted!)
        await ClearHandAsync(pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var roastFodder = combatState.CreateCard<CollectorDefend>(player);
        var roast = combatState.CreateCard<Roast>(player);
        await CardPileCmd.Add(roastFodder, PileType.Hand);
        await CardPileCmd.Add(roast, PileType.Hand);
        int hpBeforeRoast = enemy.CurrentHp;
        await CardCmd.AutoPlay(ctx, roast, enemy, AutoPlayType.Default);
        AutoTestAssert.True(enemy.CurrentHp < hpBeforeRoast, "Roast dealt damage");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(roastFodder), "Roast Pyred fodder card");

        var leech = combatState.CreateCard<SpiritLeech>(player);
        await CardPileCmd.Add(leech, PileType.Hand);
        AutoTestAssert.True(CollectorMechanics.IsAfflicted(enemy), "Enemy is Afflicted before SpiritLeech");
        await CardCmd.AutoPlay(ctx, leech, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(11, player.Creature.Block, "SpiritLeech granted 11 Block against Afflicted enemy");

        // 9. ScorchingRay (X-cost: 3 Energy -> 3 hits of 8 dmg!)
        await ClearHandAsync(pcs);
        await PowerCmd.Remove<VulnerablePower>(enemy);
        pcs.Energy = 3;
        int hpBeforeRay = enemy.CurrentHp;
        var ray = combatState.CreateCard<ScorchingRay>(player);
        await CardPileCmd.Add(ray, PileType.Hand);
        AutoTestAssert.True(ray.TryManualPlay(null), "Play ScorchingRay at X=3");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeRay - 24, enemy.CurrentHp, "ScorchingRay at X=3 dealt 3 x 8 = 24 damage");

        // 10. Wildfire (4 dmg per unique Debuff on enemy! Enemy has Weak + Doom + Demise = 3 debuffs -> 12 dmg!)
        pcs.Energy = 10;
        int debuffs = enemy.Powers.Count(p => p.Type == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff);
        AutoTestAssert.True(debuffs >= 2, $"Enemy has at least 2 debuffs before Wildfire ({debuffs})");
        int hpBeforeWildfire = enemy.CurrentHp;
        var wildfire = combatState.CreateCard<Wildfire>(player);
        await CardPileCmd.Add(wildfire, PileType.Hand);
        await CardCmd.AutoPlay(ctx, wildfire, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(hpBeforeWildfire - debuffs * 4, enemy.CurrentHp, $"Wildfire dealt {debuffs} x 4 = {debuffs * 4} damage");

        // 11. Torchbearer (10 Temp HP, Exhaust) & Misdirect (2 Block + NextTurnReserve 1)
        int tempBefore = player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>();
        var torchbearer = combatState.CreateCard<Torchbearer>(player);
        await CardPileCmd.Add(torchbearer, PileType.Hand);
        await CardCmd.AutoPlay(ctx, torchbearer, null, AutoPlayType.Default);
        AutoTestAssert.Equal(tempBefore + 10, player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>(), "Torchbearer granted 10 Temp HP");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(torchbearer), "Torchbearer Exhausted");

        var misdirect = combatState.CreateCard<Misdirect>(player);
        await CardPileCmd.Add(misdirect, PileType.Hand);
        await CardCmd.AutoPlay(ctx, misdirect, null, AutoPlayType.Default);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<NextTurnReservePower>(), "Misdirect applied NextTurnReservePower(1)");

        // 12. SapStrength ("Deathmark": 7 dmg -> LoseHpNextTurnPower(14) triggers at end of enemy turn!)
        //     & SomberShield (Pyre CollectorStrike -> AddCopyNextTurnPower adds CollectorStrike next turn!)
        await PowerCmd.Remove<CollectorDoomPower>(enemy);
        await ClearHandAsync(pcs);
        var sap = combatState.CreateCard<SapStrength>(player);
        await CardPileCmd.Add(sap, PileType.Hand);
        await CardCmd.AutoPlay(ctx, sap, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(14, enemy.GetPowerAmount<LoseHpNextTurnPower>(), "SapStrength applied LoseHpNextTurnPower(14) = 2x7 unblocked damage");

        var somberFodder1 = combatState.CreateCard<AshenStrike>(player);
        var somber1 = combatState.CreateCard<SomberShield>(player);
        await CardPileCmd.Add(somberFodder1, PileType.Hand);
        await CardPileCmd.Add(somber1, PileType.Hand);
        await CardCmd.AutoPlay(ctx, somber1, null, AutoPlayType.Default);

        // Stack a second SomberShield to confirm AddCopyNextTurnPower returns BOTH queued cards!
        var somberFodder2 = combatState.CreateCard<Roast>(player);
        var somber2 = combatState.CreateCard<SomberShield>(player);
        await CardPileCmd.Add(somberFodder2, PileType.Hand);
        await CardPileCmd.Add(somber2, PileType.Hand);
        await CardCmd.AutoPlay(ctx, somber2, null, AutoPlayType.Default);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<AddCopyNextTurnPower>(), "Two SomberShield plays stacked AddCopyNextTurnPower to 2");

        // Also apply Arrogance unused before ending turn to confirm NextAttackAppliesDoomPower expires at turn end!
        var arroganceUnused = combatState.CreateCard<Arrogance>(player);
        await CardPileCmd.Add(arroganceUnused, PileType.Hand);
        await CardCmd.AutoPlay(ctx, arroganceUnused, null, AutoPlayType.Default);
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<NextAttackAppliesDoomPower>(), "Unused Arrogance applied NextAttackAppliesDoomPower(4)");

        int hpBeforeDeathmark = enemy.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(hpBeforeDeathmark - 14, enemy.CurrentHp, "LoseHpNextTurnPower dealt 14 HP loss at end of enemy turn");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<LoseHpNextTurnPower>(), "LoseHpNextTurnPower removed itself after triggering");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<NextAttackAppliesDoomPower>(), "Unused NextAttackAppliesDoomPower expired at end of turn");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is AshenStrike), "AddCopyNextTurnPower added copy of Pyred AshenStrike into Hand next turn");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is Roast), "AddCopyNextTurnPower added copy of second Pyred card (Roast) into Hand next turn");
        AutoTestAssert.True(!player.Creature.HasPower<AddCopyNextTurnPower>(), "AddCopyNextTurnPower removed itself after triggering");
    }

    public static async Task TestUncommonBatch1InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheCollector>("COLLECTOR_10C1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy");
        var ctx = new BlockingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000m);

        static async Task ClearHandAsync(PlayerCombatState p)
        {
            foreach (var c in p.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
        }

        // 1. Billow (18 Block + CollectorBellow next turn reading LastTurnUnusedBlock!) + CantTouchThis (2 Dex + Doom on full block!)
        await ClearHandAsync(pcs);
        pcs.Energy = 10;
        var cantTouch = combatState.CreateCard<CantTouchThis>(player);
        await CardPileCmd.Add(cantTouch, PileType.Hand);
        await CardCmd.AutoPlay(ctx, cantTouch, null, AutoPlayType.Default);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DexterityPower>(), "CantTouchThis granted 2 Dexterity");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<CantTouchThisPower>(), "CantTouchThis applied CantTouchThisPower(2)");

        var billow = combatState.CreateCard<Billow>(player);
        await CardPileCmd.Add(billow, PileType.Hand);
        await CardCmd.AutoPlay(ctx, billow, null, AutoPlayType.Default);
        AutoTestAssert.Equal(20, player.Creature.Block, "Billow granted 18 + 2 (Dex) = 20 Block");

        // Simulate enemy attack fully blocked by player's 20 Block -> CantTouchThisPower applies 2 Doom!
        await CreatureCmd.Damage(ctx, player.Creature, 5m, ValueProp.Move, enemy);
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<CollectorDoomPower>(), "CantTouchThisPower applied 2 Doom on fully blocked attack");

        // End turn -> next turn receives CollectorBellow in Hand, and LastTurnUnusedBlock > 0 -> Bellow applies Doom!
        await PowerCmd.Remove<CollectorDoomPower>(enemy);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        var bellow = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<CollectorBellow>().FirstOrDefault(), "Billow added CollectorBellow to Hand next turn");
        int unusedBlock = CollectorCombatState.Get(combatState).GetPlayer(player).LastTurnUnusedBlock;
        AutoTestAssert.True(unusedBlock > 0, $"LastTurnUnusedBlock > 0 after Billow ({unusedBlock})");
        int doomBeforeBellow = enemy.GetPowerAmount<CollectorDoomPower>();
        await CardCmd.AutoPlay(ctx, bellow, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(doomBeforeBellow + unusedBlock, enemy.GetPowerAmount<CollectorDoomPower>(), $"CollectorBellow applied {unusedBlock} Doom");

        // 2. BindingCall (6 Temp HP + TorchHeadPower RandomDoom 2 -> Attack applies +2 Doom while Temp HP > 0!)
        //    Also verify CantTouchThisPower does NOT false-trigger when 0 Block + Temp HP absorbs a hit!
        await PowerCmd.Remove<CollectorDoomPower>(enemy);
        pcs.Energy = 10;
        var bindingCall = combatState.CreateCard<BindingCall>(player);
        await CardPileCmd.Add(bindingCall, PileType.Hand);
        await CardCmd.AutoPlay(ctx, bindingCall, null, AutoPlayType.Default);
        AutoTestAssert.True(player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>() >= 6, "BindingCall granted 6 Temp HP");
        AutoTestAssert.True(player.Creature.HasPower<TorchHeadPower>(), "BindingCall applied TorchHeadPower");

        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await CreatureCmd.Damage(ctx, player.Creature, 2m, ValueProp.Move, enemy);
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<CollectorDoomPower>(), "CantTouchThisPower did NOT trigger when hit absorbed by Temp HP with 0 Block");

        var attackForTorch = combatState.CreateCard<CollectorStrike>(player);
        await CardPileCmd.Add(attackForTorch, PileType.Hand);
        await CardCmd.AutoPlay(ctx, attackForTorch, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<CollectorDoomPower>(), "TorchHeadPower applied 2 Doom on Attack while Temp HP > 0");

        // 3. BlackBindings (2 Weak, then 2 Doom per unique debuff: Weak + Doom = 2 -> +4 Doom -> 6 total!)
        var bindings = combatState.CreateCard<BlackBindings>(player);
        await CardPileCmd.Add(bindings, PileType.Hand);
        await CardCmd.AutoPlay(ctx, bindings, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "BlackBindings applied 2 Weak");
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<CollectorDoomPower>(), "BlackBindings applied 2 x 2 = 4 Doom (2 -> 6)");

        // 4. CursedWail (9 temp Strength loss + 1 permanent Strength loss on Afflicted!)
        await PowerCmd.Apply<VulnerablePower>(ctx, enemy, 2m, player.Creature, null);
        AutoTestAssert.True(CollectorMechanics.IsAfflicted(enemy), "Enemy is Afflicted before CursedWail");
        int strBeforeWail = enemy.GetPowerAmount<StrengthPower>();
        var wail = combatState.CreateCard<CursedWail>(player);
        await CardPileCmd.Add(wail, PileType.Hand);
        await CardCmd.AutoPlay(ctx, wail, null, AutoPlayType.Default);
        AutoTestAssert.Equal(strBeforeWail - 10, enemy.GetPowerAmount<StrengthPower>(), "CursedWail reduced Strength by 9 (temp) + 1 (Afflicted perm) = 10");
        AutoTestAssert.True(enemy.HasPower<DarkShacklesPower>(), "CursedWail applied DarkShacklesPower for temporary Strength loss");

        // 5. Bonfire (Pyre, Block + NextTurnReserve 1) & DarkwoodKindling+ (Unplayable, Retain, Exhaust -> NextTurnReserve 2)
        await ClearHandAsync(pcs);
        var darkwood = combatState.CreateCard<DarkwoodKindling>(player);
        darkwood.UpgradeInternal();
        AutoTestAssert.True(!darkwood.CanPlay() && darkwood.Keywords.Contains(CardKeyword.Retain), "DarkwoodKindling+ is Unplayable + Retain");
        var bonfire = combatState.CreateCard<Bonfire>(player);
        await CardPileCmd.Add(darkwood, PileType.Hand);
        await CardPileCmd.Add(bonfire, PileType.Hand);
        await CardCmd.AutoPlay(ctx, bonfire, null, AutoPlayType.Default);
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<NextTurnReservePower>(), "Bonfire Pyring DarkwoodKindling gave 1 + 2 = 3 NextTurnReserve");

        // 6. BrainDrain+ (7 HP loss + upgraded Collected card in Hand) & DoubleTrouble (NextCollectedTwicePower plays Collectible twice!)
        await ClearHandAsync(pcs);
        int hpBeforeDrain = enemy.CurrentHp;
        var drain = combatState.CreateCard<BrainDrain>(player);
        drain.UpgradeInternal();
        await CardPileCmd.Add(drain, PileType.Hand);
        await CardCmd.AutoPlay(ctx, drain, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(hpBeforeDrain - 7, enemy.CurrentHp, "BrainDrain+ dealt 7 HP loss");
        var drainedCard = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<CollectibleCard>().FirstOrDefault(), "BrainDrain+ added CollectibleCard to Hand");
        AutoTestAssert.True(drainedCard.IsUpgraded, "BrainDrain+ upgraded generated CollectibleCard");

        var doubleTrouble = combatState.CreateCard<DoubleTrouble>(player);
        await CardPileCmd.Add(doubleTrouble, PileType.Hand);
        await CardCmd.AutoPlay(ctx, doubleTrouble, null, AutoPlayType.Default);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<NextCollectedTwicePower>(), "DoubleTrouble applied NextCollectedTwicePower(1)");

        var jawCollectible = combatState.CreateCard<JawWormCard>(player); // 12 Block + 1 Strength
        await CardPileCmd.Add(jawCollectible, PileType.Hand);
        int strBeforeDoubleJaw = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(jawCollectible.TryManualPlay(null), "Play JawWormCard with DoubleTrouble active");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<NextCollectedTwicePower>(), "NextCollectedTwicePower consumed on Collectible play");
        AutoTestAssert.Equal(strBeforeDoubleJaw + 2, player.Creature.GetPowerAmount<StrengthPower>(), "DoubleTrouble played JawWormCard twice (+2 Strength)");

        // 7. Empower (X-cost: X=3 for 2 turns) stacked with CultistCard (3 turns of +1) -> next turn +4, turn after +4, 3rd turn +1!
        await ClearHandAsync(pcs);
        var cultist = combatState.CreateCard<CultistCard>(player);
        var cultistFodder = combatState.CreateCard<CollectorDefend>(player);
        await CardPileCmd.Add(cultistFodder, PileType.Hand);
        await CardPileCmd.Add(cultist, PileType.Hand);
        await CardCmd.AutoPlay(ctx, cultist, null, AutoPlayType.Default);

        pcs.Energy = 3;
        var empower = combatState.CreateCard<Empower>(player);
        await CardPileCmd.Add(empower, PileType.Hand);
        AutoTestAssert.True(empower.TryManualPlay(null), "Play Empower at X=3");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<StrengthOverTurnsPower>(), "Overlapping Cultist(3) + Empower(2) kept 3 turns remaining");
        int strBeforeEmp = player.Creature.GetPowerAmount<StrengthPower>();
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(strBeforeEmp + 4, player.Creature.GetPowerAmount<StrengthPower>(), "Turn 1 of Cultist(+1) + Empower(+3) granted +4 Strength");
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(strBeforeEmp + 8, player.Creature.GetPowerAmount<StrengthPower>(), "Turn 2 of Cultist(+1) + Empower(+3) granted +4 Strength");
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(strBeforeEmp + 9, player.Creature.GetPowerAmount<StrengthPower>(), "Turn 3 of Cultist(+1) granted +1 Strength");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<StrengthOverTurnsPower>(), "StrengthOverTurnsPower removed cleanly after final turn");

        // 8. FeelMyPain (4 HP loss per Exhaust) + Extricate ("Sever Soul": Exhaust non-Attacks in Hand + 16 dmg)
        await ClearHandAsync(pcs);
        pcs.Energy = 10;
        var fmp = combatState.CreateCard<FeelMyPain>(player);
        await CardPileCmd.Add(fmp, PileType.Hand);
        await CardCmd.AutoPlay(ctx, fmp, null, AutoPlayType.Default);
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<FeelMyPainPower>(), "FeelMyPain applied FeelMyPainPower(4)");

        var extSkill1 = combatState.CreateCard<CollectorDefend>(player);
        var extSkill2 = combatState.CreateCard<CollectorDefend>(player);
        var extAtk = combatState.CreateCard<CollectorStrike>(player);
        var extricate = combatState.CreateCard<Extricate>(player);
        await CardPileCmd.Add(extSkill1, PileType.Hand);
        await CardPileCmd.Add(extSkill2, PileType.Hand);
        await CardPileCmd.Add(extAtk, PileType.Hand);
        await CardPileCmd.Add(extricate, PileType.Hand);
        int hpBeforeExt = enemy.CurrentHp;
        await CardCmd.AutoPlay(ctx, extricate, enemy, AutoPlayType.Default);
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(extSkill1) && pcs.ExhaustPile.Cards.Contains(extSkill2), "Extricate Exhausted all non-Attacks in Hand");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(extAtk), "Extricate kept Attack in Hand");
        AutoTestAssert.True(hpBeforeExt - enemy.CurrentHp >= 16 + 8, $"Extricate + FeelMyPain(2 exhausts) dealt damage + 8 HP loss ({hpBeforeExt - enemy.CurrentHp})");

        // 9. FlameLash (Pyre 10-dmg AshenStrike -> base damage 8 -> 18!) & FleetingEmbers (Pyre + 5 Block + 2 Embers!)
        await ClearHandAsync(pcs);
        var lashFodder = combatState.CreateCard<AshenStrike>(player); // 10 base damage
        var flameLash = combatState.CreateCard<FlameLash>(player);
        await CardPileCmd.Add(lashFodder, PileType.Hand);
        await CardPileCmd.Add(flameLash, PileType.Hand);
        await CardCmd.AutoPlay(ctx, flameLash, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(18m, flameLash.DynamicVars.Damage.BaseValue, "FlameLash added Pyred AshenStrike's 10 base damage (8 -> 18)");

        await ClearHandAsync(pcs);
        var feFodder = combatState.CreateCard<CollectorDefend>(player);
        var fleeting = combatState.CreateCard<FleetingEmbers>(player);
        await CardPileCmd.Add(feFodder, PileType.Hand);
        await CardPileCmd.Add(fleeting, PileType.Hand);
        await CardCmd.AutoPlay(ctx, fleeting, null, AutoPlayType.Default);
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count(c => c is Ember), "FleetingEmbers added 2 Embers into Hand");

        // 10. GreenpyreLocus (1 to Hand + 2 copies to CombatCollection) & Forgery (8 dmg + pick copy from CombatCollection!)
        await ClearHandAsync(pcs);
        var cData = CollectorCombatState.Get(combatState).GetPlayer(player);
        cData.CombatCollection.Clear();
        var locus = combatState.CreateCard<GreenpyreLocus>(player);
        await CardPileCmd.Add(locus, PileType.Hand);
        await CardCmd.AutoPlay(ctx, locus, null, AutoPlayType.Default);
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count(c => c is CollectibleCard), "GreenpyreLocus added 1 chosen Collectible to Hand");
        AutoTestAssert.Equal(2, cData.CombatCollection.Count, "GreenpyreLocus added 2 copies to CombatCollection");
        var drawnLocusCopy = await CollectorMechanics.DrawFromCollectionAsync(ctx, player);
        AutoTestAssert.True(drawnLocusCopy is CollectibleCard && pcs.Hand.Cards.Contains(drawnLocusCopy),
            "GreenpyreLocus CombatCollection copy drew cleanly into Hand");

        await ClearHandAsync(pcs);
        var forgery = combatState.CreateCard<Forgery>(player);
        await CardPileCmd.Add(forgery, PileType.Hand);
        await CardCmd.AutoPlay(ctx, forgery, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count(c => c is CollectibleCard), "Forgery added copy of chosen card from CombatCollection to Hand");

        // 11. Finalize (24 Doom + HealIfDieThisTurnPower(6) -> killing enemy heals Collector by 6 HP!)
        await CreatureCmd.Damage(ctx, player.Creature, 20m, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        int playerHpBeforeFinalizeKill = player.Creature.CurrentHp;
        var finalize = combatState.CreateCard<Finalize>(player);
        await CardPileCmd.Add(finalize, PileType.Hand);
        await CardCmd.AutoPlay(ctx, finalize, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<HealIfDieThisTurnPower>(), "Finalize applied HealIfDieThisTurnPower(6)");
        AutoTestAssert.True(enemy.GetPowerAmount<CollectorDoomPower>() >= 24, "Finalize applied 24 Doom");

        await CreatureCmd.Damage(ctx, enemy, enemy.CurrentHp + 50, ValueProp.Unblockable | ValueProp.Unpowered, player.Creature);
        AutoTestAssert.Equal(playerHpBeforeFinalizeKill + 6, player.Creature.CurrentHp, "HealIfDieThisTurnPower healed Collector by 6 HP on enemy death");
    }

    public static async Task TestUncommonBatch2InCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheCollector>("COLLECTOR_10C2");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy");
        var ctx = new BlockingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000m);

        static async Task ClearHandAsync(PlayerCombatState p)
        {
            foreach (var c in p.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
        }

        // 1. Heatwave (5 AoE + Ember) & Hurting+ -> Exhaust -> GreaterHurting+ -> Exhaust -> GreatestHurting+ -> Exhaust -> Ember!
        await ClearHandAsync(pcs);
        pcs.Energy = 10;
        var heatwave = combatState.CreateCard<Heatwave>(player);
        await CardPileCmd.Add(heatwave, PileType.Hand);
        int hpBeforeHeat = enemy.CurrentHp;
        await CardCmd.AutoPlay(ctx, heatwave, null, AutoPlayType.Default);
        AutoTestAssert.Equal(hpBeforeHeat - 5, enemy.CurrentHp, "Heatwave dealt 5 AoE damage");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is Ember), "Heatwave added Ember to Hand");

        await ClearHandAsync(pcs);
        var hurting = combatState.CreateCard<Hurting>(player);
        hurting.UpgradeInternal();
        await CardPileCmd.Add(hurting, PileType.Hand);
        await CardCmd.Exhaust(ctx, hurting);
        var greater = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<GreaterHurting>().FirstOrDefault(), "Hurting+ Exhaust added GreaterHurting+");
        AutoTestAssert.True(greater.IsUpgraded, "GreaterHurting from Hurting+ is upgraded");
        await CardCmd.Exhaust(ctx, greater);
        var greatest = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<GreatestHurting>().FirstOrDefault(), "GreaterHurting+ Exhaust added GreatestHurting+");
        AutoTestAssert.True(greatest.IsUpgraded, "GreatestHurting from GreaterHurting+ is upgraded");
        await CardCmd.Exhaust(ctx, greatest);
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is Ember), "GreatestHurting Exhaust added Ember");

        // 2. Suffering (2 Doom on Weak/Vuln) + InflictAgony (12 dmg + 2 Weak & 2 Vuln when not Afflicted -> triggers Suffering twice = 4 Doom!)
        await ClearHandAsync(pcs);
        var suffering = combatState.CreateCard<Suffering>(player);
        await CardPileCmd.Add(suffering, PileType.Hand);
        await CardCmd.AutoPlay(ctx, suffering, null, AutoPlayType.Default);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<SufferingPower>(), "Suffering applied SufferingPower(2)");

        var agony = combatState.CreateCard<InflictAgony>(player);
        await CardPileCmd.Add(agony, PileType.Hand);
        await CardCmd.AutoPlay(ctx, agony, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "InflictAgony applied 2 Weak to non-Afflicted enemy");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "InflictAgony applied 2 Vulnerable to non-Afflicted enemy");
        AutoTestAssert.Equal(4, enemy.GetPowerAmount<CollectorDoomPower>(), "SufferingPower applied 2 + 2 = 4 Doom on Weak & Vulnerable");

        // Second InflictAgony on now-Afflicted enemy does NOT re-apply Weak/Vuln!
        var agony2 = combatState.CreateCard<InflictAgony>(player);
        await CardPileCmd.Add(agony2, PileType.Hand);
        await CardCmd.AutoPlay(ctx, agony2, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "InflictAgony did not apply Weak to already-Afflicted enemy");

        // 3. VoidArmor (10 Block + 1 Blur to EVERYONE) -> IllTakeThat (steals up to 10 Block + deals 10 dmg!)
        await ClearHandAsync(pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var voidArmor = combatState.CreateCard<VoidArmor>(player);
        await CardPileCmd.Add(voidArmor, PileType.Hand);
        await CardCmd.AutoPlay(ctx, voidArmor, null, AutoPlayType.Default);
        AutoTestAssert.Equal(10, player.Creature.Block, "VoidArmor granted 10 Block to player");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<BlurPower>(), "VoidArmor applied 1 Blur to player");
        AutoTestAssert.Equal(10, enemy.Block, "VoidArmor granted 10 Block to enemy");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<BlurPower>(), "VoidArmor applied 1 Blur to enemy");

        var steal = combatState.CreateCard<IllTakeThat>(player);
        await CardPileCmd.Add(steal, PileType.Hand);
        await CardCmd.AutoPlay(ctx, steal, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(0, enemy.Block, "IllTakeThat stole enemy's 10 Block");
        AutoTestAssert.Equal(20, player.Creature.Block, "IllTakeThat added stolen 10 Block to player");

        // 4. RagingCall (6 Temp HP + TorchHead AoE 3) -> Attack triggers 3 AoE damage!
        var ragingCall = combatState.CreateCard<RagingCall>(player);
        await CardPileCmd.Add(ragingCall, PileType.Hand);
        await CardCmd.AutoPlay(ctx, ragingCall, null, AutoPlayType.Default);
        AutoTestAssert.True(player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>() >= 6, "RagingCall granted 6 Temp HP");
        int hpBeforeRagingAtk = enemy.CurrentHp;
        var strikeForRaging = combatState.CreateCard<CollectorStrike>(player); // 6 dmg + 3 AoE (plus Vuln on Strike!)
        await CardPileCmd.Add(strikeForRaging, PileType.Hand);
        await CardCmd.AutoPlay(ctx, strikeForRaging, enemy, AutoPlayType.Default);
        AutoTestAssert.True(hpBeforeRagingAtk - enemy.CurrentHp >= 9 + 3, $"RagingCall TorchHead dealt 3 AoE on Attack (total dmg={hpBeforeRagingAtk - enemy.CurrentHp})");

        // 5. SeedOfDoubt (in Hand while RotwoodKindling & OakbrimKindling are Exhausted -> grows Doom 3 -> 5 -> 7!)
        await ClearHandAsync(pcs);
        await PowerCmd.Remove<CollectorDoomPower>(enemy);
        var seed = combatState.CreateCard<SeedOfDoubt>(player);
        seed.UpgradeInternal(); // +2 Doom per Exhaust while in Hand
        var rotwood = combatState.CreateCard<RotwoodKindling>(player);
        var oakbrim = combatState.CreateCard<OakbrimKindling>(player);
        await CardPileCmd.Add(seed, PileType.Hand);
        await CardPileCmd.Add(rotwood, PileType.Hand);
        await CardPileCmd.Add(oakbrim, PileType.Hand);
        int handBeforeOak = pcs.Hand.Cards.Count;
        await CardCmd.Exhaust(ctx, oakbrim); // draws 2 cards, SeedOfDoubt +2 -> 5
        AutoTestAssert.True(pcs.Hand.Cards.Count >= handBeforeOak, "OakbrimKindling Exhaust drew cards");
        int vulnBeforeRot = enemy.GetPowerAmount<VulnerablePower>();
        await CardCmd.Exhaust(ctx, rotwood); // applies 2 Vuln + 4 Doom (plus Suffering +2 Doom!), SeedOfDoubt +2 -> 7
        AutoTestAssert.Equal(vulnBeforeRot + 2, enemy.GetPowerAmount<VulnerablePower>(), "RotwoodKindling applied 2 Vulnerable to all enemies");
        int doomBeforeSeed = enemy.GetPowerAmount<CollectorDoomPower>();
        await CardCmd.AutoPlay(ctx, seed, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(doomBeforeSeed + 7, enemy.GetPowerAmount<CollectorDoomPower>(), "SeedOfDoubt+ grew from 3 -> 7 Doom after 2 Exhausts in Hand");

        // 6. MiniCurse+ (Pyre, AllEnemies, 1 Weak + 1 Vuln) & Soulforge (Draw from Collection + 1 Upgraded copy!) & ShadowDaggers!
        await ClearHandAsync(pcs);
        var mcFodder = combatState.CreateCard<CollectorDefend>(player);
        var miniCurse = combatState.CreateCard<MiniCurse>(player);
        miniCurse.UpgradeInternal();
        AutoTestAssert.Equal(TargetType.AllEnemies, miniCurse.TargetType, "MiniCurse+ targets AllEnemies");
        int weakBeforeMc = enemy.GetPowerAmount<WeakPower>();
        await CardPileCmd.Add(mcFodder, PileType.Hand);
        await CardPileCmd.Add(miniCurse, PileType.Hand);
        await CardCmd.AutoPlay(ctx, miniCurse, null, AutoPlayType.Default);
        AutoTestAssert.Equal(weakBeforeMc + 1, enemy.GetPowerAmount<WeakPower>(), "MiniCurse+ applied Weak to all enemies");

        var cData = CollectorCombatState.Get(combatState).GetPlayer(player);
        cData.CombatCollection.Clear();
        cData.CombatCollection.Add(combatState.CreateCard<JawWormCard>(player));
        await ClearHandAsync(pcs);
        var soulforge = combatState.CreateCard<Soulforge>(player);
        await CardPileCmd.Add(soulforge, PileType.Hand);
        await CardCmd.AutoPlay(ctx, soulforge, null, AutoPlayType.Default);
        var jawCards = pcs.Hand.Cards.OfType<JawWormCard>().ToList();
        AutoTestAssert.Equal(2, jawCards.Count, "Soulforge drew JawWormCard and added 1 Upgraded copy into Hand");
        AutoTestAssert.True(jawCards.Any(c => c.IsUpgraded), "Soulforge copy is Upgraded");

        // Play both JawWormCards -> CollectedCardsPlayedThisCombat >= 2 -> ShadowDaggers hits >= 2 times!
        foreach (var jc in jawCards)
            await CardCmd.AutoPlay(ctx, jc, null, AutoPlayType.Default);
        AutoTestAssert.True(cData.CollectedCardsPlayedThisCombat >= 2, "Played 2 Collected cards this combat");
        await PowerCmd.Remove<VulnerablePower>(enemy);
        var daggers = combatState.CreateCard<ShadowDaggers>(player);
        int hpBeforeDaggers = enemy.CurrentHp;
        await CardPileCmd.Add(daggers, PileType.Hand);
        await CardCmd.AutoPlay(ctx, daggers, enemy, AutoPlayType.Default);
        int expectedHits = cData.CollectedCardsPlayedThisCombat;
        int strForDaggers = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.Equal(hpBeforeDaggers - expectedHits * (3 + strForDaggers) - 3, enemy.CurrentHp, $"ShadowDaggers hit {expectedHits} times for {3 + strForDaggers} each (+ 3 TorchHead AoE)");

        // 7. WhirlingFlame (15 AoE + put Discard card on top of Draw Pile!)
        await ClearHandAsync(pcs);
        var discCard = combatState.CreateCard<CollectorStrike>(player);
        await CardPileCmd.Add(discCard, PileType.Discard);
        var whirling = combatState.CreateCard<WhirlingFlame>(player);
        await CardPileCmd.Add(whirling, PileType.Hand);
        using (DownfallAutoTestRunner.PreferCards(c => ReferenceEquals(c, discCard)))
        {
            await CardCmd.AutoPlay(ctx, whirling, null, AutoPlayType.Default);
        }
        AutoTestAssert.True(pcs.DrawPile.Cards.FirstOrDefault() == discCard, "WhirlingFlame moved chosen Discard card to top of Draw Pile");

        // 8. Hoard (Pyre, Draw 2, single-turn Retain!) + Karma (Block at end of turn + Afflicted bonus!)
        //    + VoidArmor (enemy Blur retains Block across turn end!) + Ethereal Hurting end-of-turn exhaust!
        //    + LanternFlare (Pyre, 12 Doom + +3 Doom at end of enemy turn!)
        await ClearHandAsync(pcs);
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        await PowerCmd.Remove<BlurPower>(player.Creature);
        await PowerCmd.Remove<BlurPower>(enemy);
        await PowerCmd.Apply<WeakPower>(ctx, enemy, 2m, player.Creature, null);
        await PowerCmd.Apply<VulnerablePower>(ctx, enemy, 2m, player.Creature, null);
        var karma = combatState.CreateCard<Karma>(player);
        await CardPileCmd.Add(karma, PileType.Hand);
        await CardCmd.AutoPlay(ctx, karma, null, AutoPlayType.Default);
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<KarmaPower>(), "Karma applied KarmaPower(2)");

        // Play VoidArmor right before ending turn so both player (with Karma bonus) & enemy have BlurPower(1) across turn end!
        var voidArmorForTurnEnd = combatState.CreateCard<VoidArmor>(player);
        await CardPileCmd.Add(voidArmorForTurnEnd, PileType.Hand);
        await CardCmd.AutoPlay(ctx, voidArmorForTurnEnd, null, AutoPlayType.Default);
        AutoTestAssert.Equal(10, enemy.Block, "Enemy has 10 Block from VoidArmor before turn end");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<BlurPower>(), "Enemy has 1 Blur from VoidArmor before turn end");

        var lfFodder = combatState.CreateCard<CollectorDefend>(player);
        var lantern = combatState.CreateCard<LanternFlare>(player);
        await CardPileCmd.Add(lfFodder, PileType.Hand);
        await CardPileCmd.Add(lantern, PileType.Hand);
        await CardCmd.AutoPlay(ctx, lantern, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(3, enemy.GetPowerAmount<LanternFlarePower>(), "LanternFlare applied LanternFlarePower(3)");

        await ClearHandAsync(pcs);
        var retainTarget1 = combatState.CreateCard<CollectorStrike>(player);
        var retainTarget2 = combatState.CreateCard<CollectorDefend>(player);
        await CardPileCmd.Add(retainTarget1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(retainTarget2, PileType.Draw, CardPilePosition.Top);
        var hoardFodder = combatState.CreateCard<CollectorDefend>(player);
        var hoard = combatState.CreateCard<Hoard>(player);
        await CardPileCmd.Add(hoardFodder, PileType.Hand);
        await CardPileCmd.Add(hoard, PileType.Hand);
        await CardCmd.AutoPlay(ctx, hoard, null, AutoPlayType.Default);
        AutoTestAssert.True(pcs.Hand.Cards.Contains(retainTarget1) && pcs.Hand.Cards.Contains(retainTarget2), "Hoard drew 2 cards");

        // Also put an Ethereal Hurting in Hand to verify natural end-of-turn Ethereal Exhaust creates GreaterHurting without chain-exhausting!
        var etherealHurting = combatState.CreateCard<Hurting>(player);
        await CardPileCmd.Add(etherealHurting, PileType.Hand);

        // Neutralize enemy's attack damage for this turn so we can assert exact KarmaPower (+4 Block) + BlurPower retention!
        await PowerCmd.Apply<DarkShacklesPower>(ctx, enemy, 50m, player.Creature, null);

        int doomBeforeEndTurn = enemy.GetPowerAmount<CollectorDoomPower>();
        int greatestCountBeforeEndTurn = pcs.AllCards.Count(c => c is GreatestHurting);
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.True(pcs.Hand.Cards.Contains(retainTarget1) && pcs.Hand.Cards.Contains(retainTarget2), "Hoard retained both drawn cards across turn end");
        AutoTestAssert.Equal(doomBeforeEndTurn + 3, enemy.GetPowerAmount<CollectorDoomPower>(), "LanternFlarePower added +3 Doom at end of enemy turn");
        AutoTestAssert.Equal(10, enemy.Block, "Enemy BlurPower from VoidArmor retained enemy's 10 Block across turn end");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<BlurPower>(), "Enemy BlurPower decremented 1 -> 0 at start of enemy turn");
        AutoTestAssert.Equal(14, player.Creature.Block, "KarmaPower granted 2 + 2 (Afflicted) = 4 Block on top of VoidArmor's 10 Block, retained by BlurPower");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(etherealHurting), "Hurting Exhausted naturally via Ethereal at end of turn");
        AutoTestAssert.True(pcs.AllCards.Any(c => c is GreaterHurting && !pcs.ExhaustPile.Cards.Contains(c)), "Ethereal Hurting generated GreaterHurting without chain-exhausting it");
        AutoTestAssert.Equal(greatestCountBeforeEndTurn, pcs.AllCards.Count(c => c is GreatestHurting), "Ethereal Hurting did NOT chain-exhaust into GreatestHurting on the same turn");
    }

    public static async Task TestRareCardsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheCollector>("COLLECTOR_10D");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy");
        var ctx = new BlockingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 2000m);

        static async Task ClearHandAsync(PlayerCombatState p)
        {
            foreach (var c in p.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
        }

        // 1. FingerOfDeath (Reserves-only playability & exact deduction leaving regular Energy intact!)
        await ClearHandAsync(pcs);
        var fod = combatState.CreateCard<FingerOfDeath>(player);
        await CardPileCmd.Add(fod, PileType.Hand);
        // Turn 1 has 3 regular Energy + 1 Reserve = 4 Energy, but only 1 Reserve -> FingerOfDeath MUST be unplayable!
        AutoTestAssert.Equal(1, CollectorMechanics.GetReserves(player), "Turn 1 has 1 Reserve");
        AutoTestAssert.Equal(4, pcs.Energy, "Turn 1 has 4 total Energy");
        AutoTestAssert.True(!fod.CanPlay(), "FingerOfDeath is unplayable with only 1 Reserve despite 4 total Energy");

        // Grant +4 Reserves -> now 3 regular + 5 Reserves = 8 total Energy -> playing FingerOfDeath leaves 3 regular + 1 Reserve = 4 Energy!
        await CollectorMechanics.GainReservesAsync(ctx, player, 4);
        AutoTestAssert.Equal(5, CollectorMechanics.GetReserves(player), "Now have 5 Reserves");
        AutoTestAssert.Equal(8, pcs.Energy, "Now have 8 total Energy (3 regular + 5 Reserves)");
        AutoTestAssert.True(fod.CanPlay(), "FingerOfDeath is playable with 5 Reserves");
        AutoTestAssert.True(fod.TryManualPlay(enemy), "Play FingerOfDeath manually");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(60, enemy.GetPowerAmount<CollectorDoomPower>(), "FingerOfDeath applied 60 Doom");
        AutoTestAssert.Equal(1, CollectorMechanics.GetReserves(player), "FingerOfDeath deducted 4 Reserves (5 -> 1)");
        AutoTestAssert.Equal(4, pcs.Energy, "FingerOfDeath left 3 regular + 1 Reserve = 4 Energy");

        // Exact-boundary Reserves test: +3 Reserves -> now 3 regular + 4 Reserves = 7 Energy -> playing FingerOfDeath+ removes ReservesPower cleanly (4 -> 0) and leaves 3 regular Energy!
        await CollectorMechanics.GainReservesAsync(ctx, player, 3);
        AutoTestAssert.Equal(4, CollectorMechanics.GetReserves(player), "Now have exact 4 Reserves");
        AutoTestAssert.Equal(7, pcs.Energy, "Now have 7 total Energy (3 regular + 4 Reserves)");
        var fodPlus = combatState.CreateCard<FingerOfDeath>(player);
        CardCmd.Upgrade(fodPlus);
        await CardPileCmd.Add(fodPlus, PileType.Hand);
        AutoTestAssert.True(fodPlus.TryManualPlay(null), "Play FingerOfDeath+ with exact 4 Reserves");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(120, enemy.GetPowerAmount<CollectorDoomPower>(), "FingerOfDeath+ applied 60 AoE Doom (60 -> 120)");
        AutoTestAssert.Equal(0, CollectorMechanics.GetReserves(player), "FingerOfDeath+ consumed exact 4 Reserves (4 -> 0) cleanly");
        AutoTestAssert.Equal(3, pcs.Energy, "FingerOfDeath+ left all 3 regular Energy untouched");

        // 2. Goodbye (doubles Doom: 120 -> 240, Exhaust)
        var goodbye = combatState.CreateCard<Goodbye>(player);
        await CardPileCmd.Add(goodbye, PileType.Hand);
        await CardCmd.AutoPlay(ctx, goodbye, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(240, enemy.GetPowerAmount<CollectorDoomPower>(), "Goodbye doubled enemy Doom 120 -> 240");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(goodbye), "Goodbye Exhausted");

        // 3. ShootingStar (first Pyred Attack each turn returns as 0-cost until played; 2nd Attack Pyre does NOT re-trigger!) + CoffinNail (Exhaust -> +6 dmg copy next turn!)
        await ClearHandAsync(pcs);
        pcs.Energy = 10;
        var shootingStar = combatState.CreateCard<ShootingStar>(player);
        await CardPileCmd.Add(shootingStar, PileType.Hand);
        await CardCmd.AutoPlay(ctx, shootingStar, null, AutoPlayType.Default);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<ShootingStarPower>(), "ShootingStar applied ShootingStarPower(1)");

        var coffinNail = combatState.CreateCard<CoffinNail>(player);
        var roastPyre = combatState.CreateCard<Roast>(player);
        await CardPileCmd.Add(coffinNail, PileType.Hand);
        await CardPileCmd.Add(roastPyre, PileType.Hand);
        await CardCmd.AutoPlay(ctx, roastPyre, enemy, AutoPlayType.Default);
        var starCopy = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<CoffinNail>().FirstOrDefault(), "ShootingStar returned 0-cost copy of Pyred Attack (CoffinNail)");
        AutoTestAssert.Equal(0, starCopy.EnergyCost.GetWithModifiers(CostModifiers.All), "ShootingStar copy costs 0 until played");
        AutoTestAssert.True(player.Creature.HasPower<AddCopyNextTurnPower>(), "CoffinNail Exhaust queued upgraded copy for next turn");

        // Second Attack Pyre in the same turn must NOT re-trigger ShootingStarPower(1)!
        await ClearHandAsync(pcs);
        var secondAtkFodder = combatState.CreateCard<CollectorStrike>(player);
        var roastPyre2 = combatState.CreateCard<Roast>(player);
        await CardPileCmd.Add(secondAtkFodder, PileType.Hand);
        await CardPileCmd.Add(roastPyre2, PileType.Hand);
        await CardCmd.AutoPlay(ctx, roastPyre2, enemy, AutoPlayType.Default);
        AutoTestAssert.True(!pcs.Hand.Cards.Any(c => c is CollectorStrike), "ShootingStarPower(1) did not re-trigger on 2nd Attack Pyre in same turn");

        // 4. ProtectingCall (6 Temp HP + TorchHead Block 2) & TorchHeadCard (0-cost random Torch Head Call) & Whomp (12 dmg + 12 Temp HP)
        await ClearHandAsync(pcs);
        var protecting = combatState.CreateCard<ProtectingCall>(player);
        await CardPileCmd.Add(protecting, PileType.Hand);
        await CardCmd.AutoPlay(ctx, protecting, null, AutoPlayType.Default);
        AutoTestAssert.True(player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>() >= 6, "ProtectingCall granted 6 Temp HP");
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var strikeForProt = combatState.CreateCard<CollectorStrike>(player);
        await CardPileCmd.Add(strikeForProt, PileType.Hand);
        await CardCmd.AutoPlay(ctx, strikeForProt, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(2, player.Creature.Block, "ProtectingCall TorchHead granted 2 Block on Attack");

        var thCard = combatState.CreateCard<TorchHeadCard>(player);
        await CardPileCmd.Add(thCard, PileType.Hand);
        await CardCmd.AutoPlay(ctx, thCard, null, AutoPlayType.Default);
        var generatedCall = AutoTestAssert.NotNull(
            pcs.Hand.Cards.FirstOrDefault(c => c is BindingCall or ProtectingCall or RagingCall),
            "TorchHeadCard generated a Torch Head Call into Hand");
        AutoTestAssert.Equal(0, generatedCall.EnergyCost.GetWithModifiers(CostModifiers.All), "TorchHeadCard Call costs 0 this turn");

        int tempBeforeWhomp = player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>();
        var whomp = combatState.CreateCard<Whomp>(player);
        await CardPileCmd.Add(whomp, PileType.Hand);
        await CardCmd.AutoPlay(ctx, whomp, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(tempBeforeWhomp + 12, player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>(), "Whomp granted 12 Temp HP");

        // 5. DarkApotheosis (upgrades CombatCollection) + Omen (+1 Strength on Collected play) + HoardersStrike (plays top of Collection!)
        //    + DragonsTrove (Pyre, draws 2 from Collection + 1 Reserve)
        await ClearHandAsync(pcs);
        var cData = CollectorCombatState.Get(combatState).GetPlayer(player);
        cData.CombatCollection.Clear();
        var col1 = combatState.CreateCard<JawWormCard>(player);
        var col2 = combatState.CreateCard<SentryCard>(player);
        var col3 = combatState.CreateCard<BronzeOrbCard>(player);
        cData.CombatCollection.AddRange([col1, col2, col3]);

        var darkApoth = combatState.CreateCard<DarkApotheosis>(player);
        await CardPileCmd.Add(darkApoth, PileType.Hand);
        await CardCmd.AutoPlay(ctx, darkApoth, null, AutoPlayType.Default);
        AutoTestAssert.True(cData.CombatCollection.All(c => c.IsUpgraded), "DarkApotheosis upgraded all cards in CombatCollection");

        var omen = combatState.CreateCard<Omen>(player);
        await CardPileCmd.Add(omen, PileType.Hand);
        await CardCmd.AutoPlay(ctx, omen, null, AutoPlayType.Default);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<OmenPower>(), "Omen applied OmenPower(1)");

        int strBeforeHoarder = player.Creature.GetPowerAmount<StrengthPower>();
        var hoarder = combatState.CreateCard<HoardersStrike>(player);
        await CardPileCmd.Add(hoarder, PileType.Hand);
        await CardCmd.AutoPlay(ctx, hoarder, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(2, cData.CombatCollection.Count, "HoardersStrike popped top card (JawWormCard+) from CombatCollection");
        AutoTestAssert.Equal(strBeforeHoarder + 2 + 1, player.Creature.GetPowerAmount<StrengthPower>(), "JawWormCard+ (+2 Str) + Omen (+1 Str) triggered via HoardersStrike");

        int resBeforeTrove = CollectorMechanics.GetReserves(player);
        var troveFodder = combatState.CreateCard<SunbloomKindling>(player); // Exhaust -> +2 Strength + 2 Embers!
        var trove = combatState.CreateCard<DragonsTrove>(player);
        await CardPileCmd.Add(troveFodder, PileType.Hand);
        await CardPileCmd.Add(trove, PileType.Hand);
        int strBeforeSunbloom = player.Creature.GetPowerAmount<StrengthPower>();
        await CardCmd.AutoPlay(ctx, trove, null, AutoPlayType.Default);
        AutoTestAssert.Equal(strBeforeSunbloom + 2, player.Creature.GetPowerAmount<StrengthPower>(), "SunbloomKindling Exhaust granted +2 Strength");
        AutoTestAssert.True(pcs.Hand.Cards.Count(c => c is Ember) >= 2, "SunbloomKindling Exhaust added 2 Embers");
        AutoTestAssert.Equal(0, cData.CombatCollection.Count, "DragonsTrove drew remaining 2 cards from CombatCollection");
        AutoTestAssert.Equal(resBeforeTrove + 1, CollectorMechanics.GetReserves(player), "DragonsTrove granted +1 Reserve");

        // 6. Darkstorm (1 Blightning to Hand + 2 to Draw) & Blightning (6 Doom + 6 dmg + draw from Collection)
        await ClearHandAsync(pcs);
        cData.CombatCollection.Add(combatState.CreateCard<ByrdCard>(player));
        var darkstorm = combatState.CreateCard<Darkstorm>(player);
        await CardPileCmd.Add(darkstorm, PileType.Hand);
        await CardCmd.AutoPlay(ctx, darkstorm, null, AutoPlayType.Default);
        var blightning = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<Blightning>().FirstOrDefault(), "Darkstorm added 1 Blightning to Hand");
        AutoTestAssert.Equal(2, pcs.DrawPile.Cards.Count(c => c is Blightning), "Darkstorm shuffled 2 Blightnings into Draw Pile");
        int doomBeforeBlight = enemy.GetPowerAmount<CollectorDoomPower>();
        await CardCmd.AutoPlay(ctx, blightning, enemy, AutoPlayType.Default);
        AutoTestAssert.Equal(doomBeforeBlight + 6, enemy.GetPowerAmount<CollectorDoomPower>(), "Blightning applied 6 Doom");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is ByrdCard), "Blightning drew ByrdCard from CombatCollection");

        // 7. ThornWhip (6 AoE + 3 Bruise + Shiv) & ReceiveTribute (Pyre, choose 1 of 2 other-villain Rare cards with Retain)
        await ClearHandAsync(pcs);
        var whip = combatState.CreateCard<ThornWhip>(player);
        await CardPileCmd.Add(whip, PileType.Hand);
        await CardCmd.AutoPlay(ctx, whip, null, AutoPlayType.Default);
        AutoTestAssert.Equal(3, enemy.GetPowerAmount<Downfall.Hermit.Powers.BruisePower>(), "ThornWhip applied 3 Bruise");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is MegaCrit.Sts2.Core.Models.Cards.Shiv), "ThornWhip added Shiv to Hand");

        var tribute = combatState.CreateCard<ReceiveTribute>(player);
        await CardPileCmd.Add(tribute, PileType.Hand);
        await CardCmd.AutoPlay(ctx, tribute, null, AutoPlayType.Default);
        var tributeCard = AutoTestAssert.NotNull(
            pcs.Hand.Cards.FirstOrDefault(c => c.Rarity == CardRarity.Rare && c is not CollectorCard),
            "ReceiveTribute added an off-class Downfall Rare card to Hand");
        AutoTestAssert.True(tributeCard.Keywords.Contains(CardKeyword.Retain), "ReceiveTribute card has Retain");

        // 8. CollectorFiendFire (Exhausts Hand & deals 7 dmg per card Exhausted)
        await ClearHandAsync(pcs);
        var ff1 = combatState.CreateCard<CollectorDefend>(player);
        var ff2 = combatState.CreateCard<CollectorDefend>(player);
        var fiendFire = combatState.CreateCard<CollectorFiendFire>(player);
        await CardPileCmd.Add(ff1, PileType.Hand);
        await CardPileCmd.Add(ff2, PileType.Hand);
        await CardPileCmd.Add(fiendFire, PileType.Hand);
        int hpBeforeFiend = enemy.CurrentHp;
        await CardCmd.AutoPlay(ctx, fiendFire, enemy, AutoPlayType.Default);
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(ff1) && pcs.ExhaustPile.Cards.Contains(ff2), "CollectorFiendFire Exhausted Hand");
        AutoTestAssert.True(hpBeforeFiend - enemy.CurrentHp >= 14, $"CollectorFiendFire dealt 2 hits ({hpBeforeFiend - enemy.CurrentHp} dmg)");

        // 9. Pyromancy (CracklePower -> +1 Reserve at start of turn) + DarkLordForm+ (plays YouAreMine+ at start of turn!)
        //    + StashAway (X=2 -> 2x4 Block + 2 NextTurnReserve!)
        await ClearHandAsync(pcs);
        await PowerCmd.Remove<CollectorDoomPower>(enemy);
        await PowerCmd.Remove<WeakPower>(enemy);
        await PowerCmd.Remove<VulnerablePower>(enemy);
        var pyroFodder = combatState.CreateCard<CollectorDefend>(player);
        var pyro = combatState.CreateCard<Pyromancy>(player);
        await CardPileCmd.Add(pyroFodder, PileType.Hand);
        await CardPileCmd.Add(pyro, PileType.Hand);
        await CardCmd.AutoPlay(ctx, pyro, null, AutoPlayType.Default);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<CracklePower>(), "Pyromancy applied CracklePower(1)");

        var dlf = combatState.CreateCard<DarkLordForm>(player);
        CardCmd.Upgrade(dlf);
        await CardPileCmd.Add(dlf, PileType.Hand);
        await CardCmd.AutoPlay(ctx, dlf, null, AutoPlayType.Default);
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DarkLordFormPowerPlus>(), "DarkLordForm+ applied DarkLordFormPowerPlus(1)");

        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        // Spend down to 2 Energy and play StashAway manually at X=2!
        pcs.Energy = 2;
        var stash = combatState.CreateCard<StashAway>(player);
        await CardPileCmd.Add(stash, PileType.Hand);
        AutoTestAssert.True(stash.TryManualPlay(null), "Play StashAway at X=2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(8, player.Creature.Block, "StashAway at X=2 granted 2 x 4 = 8 Block");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<NextTurnReservePower>(), "StashAway at X=2 applied NextTurnReservePower(2)");

        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        // Next turn: NextTurnReserve(2) + Crackle(1) = 3 Reserves! DarkLordForm+ played YouAreMine+ (1 Weak, 1 Vuln, 8 Doom)! CoffinNail+6 returned to Hand!
        AutoTestAssert.True(CollectorMechanics.GetReserves(player) >= 3, $"Next turn Reserves >= 3 from StashAway(2) + Crackle(1) (got {CollectorMechanics.GetReserves(player)})");
        AutoTestAssert.True(CollectorMechanics.IsAfflicted(enemy), "DarkLordForm+ played YouAreMine+ making enemy Afflicted");
        AutoTestAssert.Equal(8, enemy.GetPowerAmount<CollectorDoomPower>(), "DarkLordForm+ YouAreMine+ applied 8 Doom");
        AutoTestAssert.True(!pcs.DiscardPile.Cards.Any(c => c is YouAreMine), "DarkLordForm+ did not bloat Discard Pile with YouAreMine+");
        var returnedCoffin = AutoTestAssert.NotNull(pcs.Hand.Cards.OfType<CoffinNail>().FirstOrDefault(), "CoffinNail returned to Hand next turn via AddCopyNextTurnPower");
        AutoTestAssert.Equal(12m, returnedCoffin.DynamicVars.Damage.BaseValue, "Returned CoffinNail has 6 + 6 = 12 base damage");
    }

    public static async Task TestRelicsAndPotionsInCombat()
    {
        var (player, _, combatState) = await DownfallAutoTestRunner.StartCombatAsync<TheCollector>("COLLECTOR_10E");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy");
        var ctx = new BlockingPlayerChoiceContext();

        await CreatureCmd.SetMaxAndCurrentHp(enemy, 1000m);

        static async Task ClearHandAsync(PlayerCombatState p)
        {
            foreach (var c in p.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
        }

        // 1. Verify CollectorRelicPool composition: EmeraldTorch + PrismaticTorch (Starter) + 11 Pool Relics (0 Boss relics!)
        var collectorRelics = ModelDb.RelicPool<CollectorRelicPool>().AllRelics.ToList();
        AutoTestAssert.Equal(13, collectorRelics.Count, "CollectorRelicPool has 2 Starter + 11 Pool = 13 relics");
        AutoTestAssert.True(collectorRelics.All(r => r.Rarity != MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Ancient && r.Rarity != MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.None),
            "CollectorRelicPool contains no non-upgrade Boss/invalid relics");

        // 2. Pickup relics: SoullitLamp (+3 Essence), TheContract (+10 Essence), ForbiddenFruit (+Injury + 10 Max HP), BottledCollectible!
        var runData = CollectorRunState.GetOrCreate(player);
        int essence0 = runData.Essence;
        await RelicCmd.Obtain<SoullitLamp>(player);
        AutoTestAssert.Equal(essence0 + 3, runData.Essence, "SoullitLamp granted +3 Essence on pickup");

        await RelicCmd.Obtain<TheContract>(player);
        AutoTestAssert.Equal(essence0 + 13, runData.Essence, "TheContract granted +10 Essence on pickup");

        int maxHp0 = player.Creature.MaxHp;
        await RelicCmd.Obtain<ForbiddenFruit>(player);
        AutoTestAssert.Equal(maxHp0 + 10, player.Creature.MaxHp, "ForbiddenFruit granted +10 Max HP");
        AutoTestAssert.True(player.Deck.Cards.Any(c => c is MegaCrit.Sts2.Core.Models.Cards.Injury), "ForbiddenFruit added Injury Curse to Deck");

        // Add 2 collectibles to run Collection and test BottledCollectible picking index 1 (SentryCard -> Upgraded & Bottled!)
        runData.AddToCollection(player, combatState.CreateCard<JawWormCard>(player));
        runData.AddToCollection(player, combatState.CreateCard<SentryCard>(player));
        using (DownfallAutoTestRunner.PreferCards(c => c is SentryCard))
        {
            await RelicCmd.Obtain<BottledCollectible>(player);
        }
        AutoTestAssert.Equal(1, runData.BottledCollectionIndex, "BottledCollectible bottled SentryCard at index 1");
        AutoTestAssert.Equal(1, runData.Collection[1].UpgradeLevel, "BottledCollectible upgraded SentryCard");

        // 3. Turn-1 start relics (verify directly on Round 1 via Obtain + invoking AfterPlayerTurnStart):
        //    AutoCurser (1 Weak + 1 Vuln), Bagpipes (+1 to Weak/Vuln applied -> 2 Weak + 2 Vuln!), BagOfTricks (draw 2 Collected),
        //    SoullitLamp (Ember), ThimbleHelm (3 Temp HP + MoreBlockWithTempHPPower(1)), ForbiddenFruit (1 Dexterity)!
        await ClearHandAsync(pcs);
        CollectorMechanics.InitializeCombatCollection(combatState, player);
        await RelicCmd.Obtain<Bagpipes>(player);
        await RelicCmd.Obtain<AutoCurser>(player);
        await RelicCmd.Obtain<BagOfTricks>(player);
        await RelicCmd.Obtain<ThimbleHelm>(player);

        // Fire Turn 1 start hooks on the newly obtained relics while combatState.RoundNumber == 1!
        foreach (var relic in player.Relics.Where(r => r is AutoCurser or BagOfTricks or SoullitLamp or ThimbleHelm or ForbiddenFruit).ToList())
            await relic.AfterPlayerTurnStart(ctx, player);

        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "AutoCurser + Bagpipes applied 1 + 1 = 2 Weak");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "AutoCurser + Bagpipes applied 1 + 1 = 2 Vulnerable");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is Ember), "SoullitLamp added Ember to Hand on Turn 1");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is SentryCard { IsUpgraded: true }), "BagOfTricks drew bottled SentryCard+");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is JawWormCard), "BagOfTricks drew 2nd Collected card (JawWormCard)");
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>(), "ThimbleHelm granted 3 Temp HP");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<MoreBlockWithTempHPPower>(), "ThimbleHelm applied MoreBlockWithTempHPPower(1)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DexterityPower>(), "ForbiddenFruit granted 1 Dexterity");

        // Test MoreBlockWithTempHPPower: CollectorDefend (5 base + 1 Dex + 1 ThimbleHelm = 7 Block!)
        await CreatureCmd.LoseBlock(ctx, player.Creature, player.Creature.Block, null);
        var defendWithTemp = combatState.CreateCard<CollectorDefend>(player);
        await CardPileCmd.Add(defendWithTemp, PileType.Hand);
        await CardCmd.AutoPlay(ctx, defendWithTemp, null, AutoPlayType.Default);
        AutoTestAssert.Equal(7, player.Creature.Block, "CollectorDefend gained 5 + 1 (Dex) + 1 (ThimbleHelm w/ Temp HP) = 7 Block");

        // 4. HolidayCoal (empty Collection draw -> LuckyWick!) & JadeRing (+6 Doom damage!)
        await RelicCmd.Obtain<HolidayCoal>(player);
        await RelicCmd.Obtain<JadeRing>(player);
        var cData = CollectorCombatState.Get(combatState).GetPlayer(player);
        cData.CombatCollection.Clear();
        await ClearHandAsync(pcs);
        var coalCard = await CollectorMechanics.DrawFromCollectionAsync(ctx, player);
        AutoTestAssert.True(coalCard is LuckyWick && pcs.Hand.Cards.Contains(coalCard), "HolidayCoal generated LuckyWick when drawing from empty Collected pile");

        // 5. All 4 Collector Potions: MiniCursePotion (Soul Extract), TempHPPotion (Felwater), ReservePotion, DebuffDoublePotion (Embalming Oils)!
        await PowerCmd.Remove<WeakPower>(enemy);
        await PowerCmd.Remove<VulnerablePower>(enemy);
        await PowerCmd.Remove<CollectorDoomPower>(enemy);

        var miniCursePot = ModelDb.Potion<Downfall.Collector.Potions.MiniCursePotion>().ToMutable();
        await PotionCmd.TryToProcure(miniCursePot, player);
        await miniCursePot.OnUseWrapper(ctx, enemy);
        // With Bagpipes (+1 to Weak/Vuln): 2 Weak, 2 Vulnerable, 6 Doom!
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "MiniCursePotion (+ Bagpipes) applied 2 Weak");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "MiniCursePotion (+ Bagpipes) applied 2 Vulnerable");
        AutoTestAssert.Equal(6, enemy.GetPowerAmount<CollectorDoomPower>(), "MiniCursePotion applied 6 Doom");

        // Also apply -2 Strength (a negative-stack Debuff) to confirm DebuffDoublePotion doubles negative debuffs too (-2 -> -4)!
        await PowerCmd.Apply<StrengthPower>(ctx, enemy, -2m, player.Creature, null);

        var doublePot = ModelDb.Potion<Downfall.Collector.Potions.DebuffDoublePotion>().ToMutable();
        await PotionCmd.TryToProcure(doublePot, player);
        await doublePot.OnUseWrapper(ctx, enemy);
        AutoTestAssert.Equal(4, enemy.GetPowerAmount<WeakPower>(), "DebuffDoublePotion doubled Weak 2 -> 4");
        AutoTestAssert.Equal(4, enemy.GetPowerAmount<VulnerablePower>(), "DebuffDoublePotion doubled Vulnerable 2 -> 4");
        AutoTestAssert.Equal(12, enemy.GetPowerAmount<CollectorDoomPower>(), "DebuffDoublePotion doubled Doom 6 -> 12");
        AutoTestAssert.Equal(-4, enemy.GetPowerAmount<StrengthPower>(), "DebuffDoublePotion doubled negative Strength -2 -> -4");

        int tempBeforePot = player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>();
        var tempPot = ModelDb.Potion<Downfall.Collector.Potions.TempHPPotion>().ToMutable();
        await PotionCmd.TryToProcure(tempPot, player);
        await tempPot.OnUseWrapper(ctx, null);
        AutoTestAssert.Equal(tempBeforePot + 8, player.Creature.GetPowerAmount<Downfall.Core.DownfallTempHpPower>(), "TempHPPotion granted 8 Temp HP");

        int resBeforePot = CollectorMechanics.GetReserves(player);
        var resPot = ModelDb.Potion<Downfall.Collector.Potions.ReservePotion>().ToMutable();
        await PotionCmd.TryToProcure(resPot, player);
        await resPot.OnUseWrapper(ctx, null);
        AutoTestAssert.Equal(resBeforePot + 2, CollectorMechanics.GetReserves(player), "ReservePotion granted 2 Reserves");

        // 6. FuelCanister (Retain lowest-cost card in Hand across turn end) + JadeRing (+6 Doom damage: 12 + 6 = 18 HP loss!)
        await RelicCmd.Obtain<FuelCanister>(player);
        await ClearHandAsync(pcs);
        var lowCostCard = combatState.CreateCard<Misdirect>(player); // 0 cost
        var highCostCard = combatState.CreateCard<Billow>(player);   // 3 cost
        await CardPileCmd.Add(lowCostCard, PileType.Hand);
        await CardPileCmd.Add(highCostCard, PileType.Hand);

        int enemyHpBeforeJadeDoom = enemy.CurrentHp;
        await DownfallAutoTestRunner.EndTurnAndWaitForRoundAsync(player, combatState.RoundNumber + 1);
        AutoTestAssert.Equal(enemyHpBeforeJadeDoom - 18, enemy.CurrentHp, "JadeRing added +6 damage to 12 Doom (18 HP loss)");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(lowCostCard), "FuelCanister retained lowest-cost card (0-cost Misdirect) across turn end");
        AutoTestAssert.True(!pcs.Hand.Cards.Contains(highCostCard), "FuelCanister did NOT retain 3-cost Billow");
    }
}
