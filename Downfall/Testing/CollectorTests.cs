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
}
