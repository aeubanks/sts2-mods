using Godot;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.Unlocks;
using Packmaster.Cards;
using Packmaster.Character;
using Packmaster.Config;
using Packmaster.Packs;
using Packmaster.Relics;

namespace Packmaster.Testing;

internal static class PackmasterAutoTestRunner
{
    private static int _unhandledErrors;

    public static void Start()
    {
        _ = RunAllAsync();
    }

    private static void Log(string msg)
    {
        Console.WriteLine($"[AUTOTEST] {msg}");
        Console.Out.Flush();
    }

    private static async Task RunAllAsync()
    {
        int passed = 0;
        int failed = 0;
        int exitCode = 1;

        void OnUnobserved(object? _, UnobservedTaskExceptionEventArgs e)
        {
            Interlocked.Increment(ref _unhandledErrors);
            Log($"UNOBSERVED EXCEPTION: {e.Exception}");
            e.SetObserved();
        }

        void OnUnhandled(object _, UnhandledExceptionEventArgs e)
        {
            Interlocked.Increment(ref _unhandledErrors);
            Log($"UNHANDLED EXCEPTION: {e.ExceptionObject}");
        }

        TaskScheduler.UnobservedTaskException += OnUnobserved;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandled;

        try
        {
            Log("Waiting for NGame.GameStartupComplete...");
            await NGame.Instance!.GameStartupComplete.WaitAsync(TimeSpan.FromSeconds(45));

            NonInteractiveMode.AutoSlayerCheck = () => true;
            SaveManager.Instance.PrefsSave.FastMode = FastModeType.Instant;

            var filter = System.Environment.GetEnvironmentVariable("PACKMASTER_AUTOTEST_FILTER");
            (string Name, Func<Task> Run)[] tests =
            [
                ("ModelDbAssetsAndLocIntegrity", TestModelDbAssetsAndLocIntegrity),
                ("PackSystemAndCardPoolFiltering", TestPackSystemAndCardPoolFiltering),
                ("StartRunAndPlayStarterCombat", TestStartRunAndPlayStarterCombat),
            ];

            foreach (var (name, run) in tests)
            {
                if (!string.IsNullOrEmpty(filter) && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var errorsBefore = Volatile.Read(ref _unhandledErrors);
                try
                {
                    using var selectorScope = CardSelectorStack.SetTestSelector(new AutoSlayCardSelector(new Rng(1)));
                    Log($"RUN  {name}");
                    await run().WaitAsync(TimeSpan.FromSeconds(30));
                    AutoTestAssert.Equal(errorsBefore, Volatile.Read(ref _unhandledErrors), $"{name} triggered unhandled background exceptions");
                    passed++;
                    Log($"PASS {name}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"FAIL {name}: {ex}");
                }
                finally
                {
                    CleanupRun();
                }
            }

            var totalUnhandled = Volatile.Read(ref _unhandledErrors);
            if (failed == 0 && totalUnhandled == 0 && passed > 0)
            {
                exitCode = 0;
                Log($"SUMMARY: ALL PASSED ({passed}/{passed})");
            }
            else
            {
                exitCode = 1;
                Log($"SUMMARY: FAILED (passed={passed}, failed={failed}, unhandled={totalUnhandled})");
            }
        }
        catch (Exception ex)
        {
            Log($"FATAL RUNNER ERROR: {ex}");
            exitCode = 1;
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
            Console.Out.Flush();
            NGame.Instance?.GetTree()?.Quit(exitCode);
        }
    }

    private static void CleanupRun()
    {
        if (RunManager.Instance.IsInProgress)
        {
            RunManager.Instance.CleanUp(graceful: false);
        }
        PackRunState.Reset();
    }

    private static Task TestModelDbAssetsAndLocIntegrity()
    {
        var character = ModelDb.Character<ThePackmaster>();
        AutoTestAssert.Equal(75, character.StartingHp, "StartingHp");
        AutoTestAssert.Equal(10, character.StartingDeck.Count(), "StartingDeck count");
        AutoTestAssert.True(character.StartingRelics.Any(r => r is HandyHaversack), "HandyHaversack in StartingRelics");
        AutoTestAssert.True(character.Title.Exists(), "Character title loc missing");
        AutoTestAssert.True(!string.IsNullOrWhiteSpace(character.Title.GetFormattedText()), "Character title empty");

        var relicPool = AutoTestAssert.NotNull(ModelDb.RelicPool<PackmasterRelicPool>(), "PackmasterRelicPool");
        AutoTestAssert.NotNull(ModelDb.PotionPool<PackmasterPotionPool>(), "PackmasterPotionPool");

        string[] charAssets =
        [
            character.CustomIconTexturePath,
            character.CustomCharacterSelectIconPath,
            character.CustomCharacterSelectLockedIconPath,
            character.CustomCharacterSelectBg,
            character.CustomMapMarkerPath,
        ];
        foreach (var path in charAssets)
        {
            AutoTestAssert.True(ResourceLoader.Exists(path), $"Missing character asset: {path}");
        }

        var cardPool = ModelDb.CardPool<PackmasterCardPool>();
        AutoTestAssert.True(ResourceLoader.Exists(cardPool.BigEnergyIconPath), $"Missing {cardPool.BigEnergyIconPath}");
        AutoTestAssert.True(ResourceLoader.Exists(cardPool.TextEnergyIconPath), $"Missing {cardPool.TextEnergyIconPath}");

        var cards = cardPool.AllCards.ToList();
        AutoTestAssert.True(cards.Count >= 7, $"Expected at least 7 Packmaster cards, got {cards.Count}");

        foreach (var card in cards)
        {
            AutoTestAssert.True(ResourceLoader.Exists(card.PortraitPath), $"Missing portrait for {card.Id}: {card.PortraitPath}");
            if (card is PackmasterCard pmCard)
            {
                AutoTestAssert.True(ResourceLoader.Exists(pmCard.CustomPortraitPath), $"Missing big portrait for {card.Id}: {pmCard.CustomPortraitPath}");
            }

            var titleLoc = new LocString("cards", $"{card.Id.Entry}.title");
            AutoTestAssert.True(titleLoc.Exists(), $"Missing title loc key for {card.Id}");
            AutoTestAssert.True(card.Description.Exists(), $"Missing description loc key for {card.Id}");

            var title = card.Title;
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(title), $"Empty title for {card.Id}");

            var desc = card.GetDescriptionForPile(PileType.None);
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(desc), $"Empty description for {card.Id}");
            AutoTestAssert.True(!desc.Contains('{') && !desc.Contains('}'), $"Unresolved SmartFormat token in {card.Id}: {desc}");

            var upgradeDesc = card.GetDescriptionForUpgradePreview();
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(upgradeDesc), $"Empty upgrade description for {card.Id}");
            AutoTestAssert.True(!upgradeDesc.Contains('{') && !upgradeDesc.Contains('}'), $"Unresolved SmartFormat token in {card.Id}+: {upgradeDesc}");
        }

        var relics = relicPool.AllRelics.ToList();
        AutoTestAssert.True(relics.Count >= 1, $"Expected at least 1 Packmaster relic, got {relics.Count}");
        foreach (var relic in relics)
        {
            AutoTestAssert.True(ResourceLoader.Exists(relic.PackedIconPath), $"Missing relic icon for {relic.Id}: {relic.PackedIconPath}");
            if (relic is PackmasterRelic pmRelic)
            {
                foreach (var iconPath in pmRelic.AllIconPaths)
                    AutoTestAssert.True(ResourceLoader.Exists(iconPath), $"Missing relic asset for {relic.Id}: {iconPath}");
            }
            AutoTestAssert.True(relic.Title.Exists(), $"Missing relic title loc for {relic.Id}");
            AutoTestAssert.True(relic.DynamicDescription.Exists(), $"Missing relic description loc for {relic.Id}");
            AutoTestAssert.True(relic.Flavor.Exists(), $"Missing relic flavor loc for {relic.Id}");
            var desc = relic.DynamicDescription.GetFormattedText();
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(desc), $"Empty relic description for {relic.Id}");
            AutoTestAssert.True(!desc.Contains('{') && !desc.Contains('}'), $"Unresolved SmartFormat token in {relic.Id}: {desc}");
        }

        string[] settingsLocKeys =
        [
            "PACKMASTER-ALL_PACKS_MODE.title",
            "PACKMASTER-ALL_PACKS_MODE.hover.title",
            "PACKMASTER-ALL_PACKS_MODE.hover.desc",
            "PACKMASTER-PACKS_PER_RUN.title",
            "PACKMASTER-PACKS_PER_RUN.hover.title",
            "PACKMASTER-PACKS_PER_RUN.hover.desc",
        ];
        foreach (var key in settingsLocKeys)
        {
            AutoTestAssert.True(new LocString("settings_ui", key).Exists(), $"Missing settings_ui loc key: {key}");
        }
        AutoTestAssert.True(new LocString("gameplay_ui", "PACKMASTER-PACK_CARD_TYPE").Exists(), "Missing PACKMASTER-PACK_CARD_TYPE loc key");

        return Task.CompletedTask;
    }

    private static async Task TestPackSystemAndCardPoolFiltering()
    {
        var savedAllPacks = PackmasterConfig.AllPacksMode;
        var savedPacksPerRun = PackmasterConfig.PacksPerRun;

        using var packScope = PackRegistry.OverrideForTests(
        [
            new PackDefinition("TestCore", AlwaysInclude: true, [typeof(CommonStrike)]),
            new PackDefinition("TestAlpha", AlwaysInclude: false, [typeof(UncommonStrike)]),
            new PackDefinition("TestBeta", AlwaysInclude: false, [typeof(RareStrike)]),
        ]);

        try
        {
            PackmasterConfig.AllPacksMode = false;
            PackmasterConfig.PacksPerRun = 2;

            // 1. Seed determinism & AlwaysInclude rule.
            var roll1 = PackRunState.RollPackIdsForSeed("SEED_DET", 2);
            var roll2 = PackRunState.RollPackIdsForSeed("SEED_DET", 2);
            AutoTestAssert.Equal(2, roll1.Count, "Rolled pack count");
            AutoTestAssert.True(roll1.SequenceEqual(roll2), "RollPackIdsForSeed is deterministic");
            AutoTestAssert.True(roll1.Contains("TestCore"), "AlwaysInclude pack always present");

            var cardPool = ModelDb.CardPool<PackmasterCardPool>();
            var unlockState = new UnlockState(SaveManager.Instance.Progress);

            // 2. Outside a run (Compendium), all cards are visible.
            var compendiumCards = cardPool.GetUnlockedCards(unlockState, CardMultiplayerConstraint.None).ToList();
            AutoTestAssert.True(compendiumCards.Any(c => c is CommonStrike), "Compendium includes CommonStrike");
            AutoTestAssert.True(compendiumCards.Any(c => c is UncommonStrike), "Compendium includes UncommonStrike");
            AutoTestAssert.True(compendiumCards.Any(c => c is RareStrike), "Compendium includes RareStrike");

            // 3. Start a Packmaster run: first verify lazy auto-initialization from seed, then override to [TestCore, TestAlpha].
            var player = Player.CreateForNewRun<ThePackmaster>(unlockState, 1uL);
            var runState = await NGame.Instance!.StartNewSingleplayerRun(
                player,
                shouldSave: false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                "PMPACK01",
                GameMode.Standard,
                isCustomGame: false);

            var lazyActive = AutoTestAssert.NotNull(PackRunState.GetActivePackIdsForCurrentRun(), "Lazy active packs initialized on run");
            AutoTestAssert.Equal(2, lazyActive.Count, "Lazy active pack count matches PacksPerRun");
            AutoTestAssert.True(lazyActive.Contains("TestCore"), "Lazy active packs include AlwaysInclude pack");

            PackRunState.SetActivePacksForRun(runState, ["TestCore", "TestAlpha"]);

            var filteredCards = cardPool.GetUnlockedCards(unlockState, CardMultiplayerConstraint.SingleplayerOnly).ToList();
            AutoTestAssert.True(filteredCards.Any(c => c is PackmasterStrike), "Filtered pool includes Basic Strike");
            AutoTestAssert.True(filteredCards.Any(c => c is CommonStrike), "Filtered pool includes active TestCore card");
            AutoTestAssert.True(filteredCards.Any(c => c is UncommonStrike), "Filtered pool includes active TestAlpha card");
            AutoTestAssert.True(!filteredCards.Any(c => c is RareStrike), "Filtered pool excludes inactive TestBeta card");

            // 3b. Verify ICustomTypeTextCard plaque modifier renders pack name.
            var plaqueMod = AutoTestAssert.NotNull(
                ((PackmasterCard)ModelDb.Card<CommonStrike>()).GetTypeModifiers().FirstOrDefault(),
                "CommonStrike type plaque modifier");
            plaqueMod.Add("Type", "Attack");
            AutoTestAssert.True(plaqueMod.GetFormattedText().Contains("TestCore"), "Type plaque contains pack name");
            AutoTestAssert.True(!((PackmasterCard)ModelDb.Card<PackmasterStrike>()).GetTypeModifiers().Any(), "Basic Strike has no pack type modifier");

            // 4. CardFactory reward generation must never offer inactive TestBeta cards (request 2 since only 2 active non-Basic cards exist).
            var rewardOptions = new CardCreationOptions([cardPool], CardCreationSource.Encounter, CardRarityOddsType.RegularEncounter);
            for (int i = 0; i < 10; i++)
            {
                var rewards = CardFactory.CreateForReward(player, 2, rewardOptions).ToList();
                AutoTestAssert.Equal(2, rewards.Count, "Reward card count");
                AutoTestAssert.True(rewards.All(r => r.Card is not RareStrike), "Reward must not contain inactive pack card");
            }

            // 5. AllPacksMode unlocks all packs immediately.
            PackmasterConfig.AllPacksMode = true;
            var allModeCards = cardPool.GetUnlockedCards(unlockState, CardMultiplayerConstraint.SingleplayerOnly).ToList();
            AutoTestAssert.True(allModeCards.Any(c => c is RareStrike), "AllPacksMode includes TestBeta card");
            PackmasterConfig.AllPacksMode = false;

            // 6. Test PackHelper.HasSynergy in real combat.
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, showTransition: false);
            await WaitUntilAsync(() =>
                CombatManager.Instance.IsInProgress
                && !CombatManager.Instance.PlayerActionsDisabled
                && player.PlayerCombatState is not null
                && !RunManager.Instance.ActionExecutor.IsRunning);

            var combatState = AutoTestAssert.NotNull(player.Creature.CombatState, "CombatState for synergy test");
            // Leave 2 Basic cards in hand so hand has plenty of room (max hand size is 10).
            foreach (var starterCard in player.PlayerCombatState!.Hand.Cards.Skip(2).ToList())
            {
                await CardPileCmd.Add(starterCard, PileType.Discard);
            }

            var coreCard = combatState.CreateCard<CommonStrike>(player);
            var coreCard2 = combatState.CreateCard<CommonStrike>(player);
            var alphaCard = combatState.CreateCard<UncommonStrike>(player);
            var alphaCard2 = combatState.CreateCard<UncommonStrike>(player);
            var betaCard = combatState.CreateCard<RareStrike>(player);

            await CardPileCmd.Add(coreCard, PileType.Hand);
            await CardPileCmd.Add(coreCard2, PileType.Hand);
            AutoTestAssert.True(!PackHelper.HasSynergy(coreCard), "Same-pack and Basic cards do not count toward Synergy");

            await CardPileCmd.Add(alphaCard, PileType.Hand);
            await CardPileCmd.Add(alphaCard2, PileType.Hand);
            AutoTestAssert.True(!PackHelper.HasSynergy(coreCard), "2 cards from the SAME other pack still only count as 1 other pack");

            await CardPileCmd.Add(betaCard, PileType.Hand);
            AutoTestAssert.True(PackHelper.HasSynergy(coreCard), "2 other distinct packs in hand -> Synergy active!");
        }
        finally
        {
            PackmasterConfig.AllPacksMode = savedAllPacks;
            PackmasterConfig.PacksPerRun = savedPacksPerRun;
        }
    }

    private static async Task TestStartRunAndPlayStarterCombat()
    {
        var player = Player.CreateForNewRun<ThePackmaster>(new UnlockState(SaveManager.Instance.Progress), 1uL);
        await NGame.Instance!.StartNewSingleplayerRun(
            player,
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            "PMTEST01",
            GameMode.Standard,
            isCustomGame: false);

        AutoTestAssert.Equal(75, player.Creature.MaxHp, "Run player MaxHp");
        AutoTestAssert.Equal(10, player.Deck.Cards.Count, "Starter deck size");
        AutoTestAssert.True(player.Relics.Any(r => r is HandyHaversack), "Player starts with HandyHaversack");

        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, showTransition: false);

        var combatState = AutoTestAssert.NotNull(player.Creature.CombatState, "CombatState after entering monster room");
        await WaitUntilAsync(() =>
            CombatManager.Instance.IsInProgress
            && !CombatManager.Instance.PlayerActionsDisabled
            && player.PlayerCombatState is not null
            && !RunManager.Instance.ActionExecutor.IsRunning);

        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");

        // Handy Haversack: +1 draw (6 cards) and +1 energy (4 energy) on turn 1.
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "Turn-1 hand size with HandyHaversack");
        AutoTestAssert.Equal(4, pcs.Energy, "Turn-1 energy with HandyHaversack");

        // Ensure Rummage, Cardistry, and a Defend are in hand, and a 1-cost Strike is on top of the draw pile for Rummage.
        var rummage = AutoTestAssert.NotNull(pcs.AllCards.FirstOrDefault(c => c is Rummage), "Rummage in combat");
        var cardistry = AutoTestAssert.NotNull(pcs.AllCards.FirstOrDefault(c => c is Cardistry), "Cardistry in combat");
        var strikeForDraw = AutoTestAssert.NotNull(
            pcs.DrawPile.Cards.FirstOrDefault(c => c is PackmasterStrike)
                ?? pcs.AllCards.FirstOrDefault(c => c is PackmasterStrike),
            "Strike for Rummage draw");
        var defendForPlay = AutoTestAssert.NotNull(
            pcs.Hand.Cards.FirstOrDefault(c => c is PackmasterDefend)
                ?? pcs.AllCards.FirstOrDefault(c => c is PackmasterDefend),
            "Defend for Cardistry rebound");

        if (rummage.Pile?.Type != PileType.Hand)
            await CardPileCmd.Add(rummage, PileType.Hand);
        if (cardistry.Pile?.Type != PileType.Hand)
            await CardPileCmd.Add(cardistry, PileType.Hand);
        if (defendForPlay.Pile?.Type != PileType.Hand)
            await CardPileCmd.Add(defendForPlay, PileType.Hand);
        await CardPileCmd.Add(strikeForDraw, PileType.Draw, CardPilePosition.Top);

        // 1. Test Rummage: 10 damage, draws top card (strikeForDraw), reduces its cost from 1 -> 0 this turn.
        var enemyHpBefore = enemy.CurrentHp;
        AutoTestAssert.True(rummage.TryManualPlay(enemy), "Play Rummage");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(enemyHpBefore - 10, enemy.CurrentHp, "Enemy HP after Rummage");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(strikeForDraw), "Rummage drew top card into hand");
        AutoTestAssert.Equal(0, strikeForDraw.EnergyCost.GetWithModifiers(CostModifiers.All), "Rummage reduced drawn card cost to 0");

        // 2. Test Cardistry: 2 Block, applies ReboundPower (does not rebound itself), next played card goes to top of draw pile.
        AutoTestAssert.True(cardistry.TryManualPlay(null), "Play Cardistry");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.Block, "Block after Cardistry");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(cardistry), "Cardistry went to discard pile");
        AutoTestAssert.True(player.Creature.HasPower<ReboundPower>(), "Cardistry applied ReboundPower");

        // 3. Play Defend next: gains +5 Block (total 7) and rebounds onto top of Draw Pile!
        AutoTestAssert.True(defendForPlay.TryManualPlay(null), "Play Defend after Cardistry");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.Block, "Block after Cardistry + Defend");
        AutoTestAssert.True(pcs.DrawPile.Cards.FirstOrDefault() == defendForPlay, "Rebounded Defend is on top of Draw Pile");

        // 4. Play the 0-cost Rummaged Strike: deals 6 damage for 0 energy.
        var energyBeforeFreeStrike = pcs.Energy;
        var enemyHpBeforeStrike = enemy.CurrentHp;
        AutoTestAssert.True(strikeForDraw.TryManualPlay(enemy), "Play 0-cost Rummaged Strike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeFreeStrike, pcs.Energy, "0-cost Rummaged Strike spent 0 energy");
        AutoTestAssert.Equal(enemyHpBeforeStrike - 6, enemy.CurrentHp, "Enemy HP after Strike");

        // 5. End Turn 1 -> wait for Turn 2: verify HandyHaversack does NOT re-trigger, Rebound expired,
        //    rebounded Defend is in Turn-2 hand, and Rummage cost reduction expired.
        var turn2Started = new TaskCompletionSource();
        void OnTurnStarted(CombatState state)
        {
            if (state.RoundNumber == 2 && state.CurrentSide == CombatSide.Player)
                turn2Started.TrySetResult();
        }
        CombatManager.Instance.TurnStarted += OnTurnStarted;
        try
        {
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player, pcs.TurnNumber));
            await turn2Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await WaitUntilAsync(() => !CombatManager.Instance.PlayerActionsDisabled && !RunManager.Instance.ActionExecutor.IsRunning);
        }
        finally
        {
            CombatManager.Instance.TurnStarted -= OnTurnStarted;
        }

        AutoTestAssert.Equal(5, pcs.Hand.Cards.Count, "Turn-2 hand size (HandyHaversack must not re-trigger)");
        AutoTestAssert.Equal(3, pcs.Energy, "Turn-2 energy (HandyHaversack must not re-trigger)");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(defendForPlay), "Rebounded Defend was drawn on Turn 2");
        AutoTestAssert.True(!player.Creature.HasPower<ReboundPower>(), "ReboundPower expired after Turn 1");
        AutoTestAssert.Equal(1, strikeForDraw.EnergyCost.GetWithModifiers(CostModifiers.All), "Rummage cost reduction expired on Turn 2");
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 15000)
    {
        var deadline = System.Environment.TickCount64 + timeoutMs;
        while (!predicate())
        {
            if (System.Environment.TickCount64 > deadline)
                throw new TimeoutException($"Condition not met within {timeoutMs}ms.");
            await Task.Delay(50);
        }
    }
}
