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
using Packmaster.Cards.CoreSet;
using Packmaster.Character;
using Packmaster.Config;
using Packmaster.Packs;
using Packmaster.Powers;
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
                ("CoreSetCardsInCombat", TestCoreSetCardsInCombat),
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

    private static async Task<(Player Player, RunState RunState, CombatState CombatState)> StartCombatAsync(string seed)
    {
        var player = Player.CreateForNewRun<ThePackmaster>(new UnlockState(SaveManager.Instance.Progress), 1uL);
        var runState = await NGame.Instance!.StartNewSingleplayerRun(
            player,
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            isCustomGame: false);

        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, showTransition: false);
        await WaitUntilAsync(() =>
            CombatManager.Instance.IsInProgress
            && !CombatManager.Instance.PlayerActionsDisabled
            && player.PlayerCombatState is not null
            && !RunManager.Instance.ActionExecutor.IsRunning);

        var combatState = AutoTestAssert.NotNull(player.Creature.CombatState as CombatState, "CombatState after entering monster room");
        return (player, runState, combatState);
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
            AutoTestAssert.True(ResourceLoader.Exists(path), $"Missing character asset: {path}");

        var cardPool = ModelDb.CardPool<PackmasterCardPool>();
        AutoTestAssert.True(ResourceLoader.Exists(cardPool.BigEnergyIconPath), $"Missing {cardPool.BigEnergyIconPath}");
        AutoTestAssert.True(ResourceLoader.Exists(cardPool.TextEnergyIconPath), $"Missing {cardPool.TextEnergyIconPath}");

        var cards = cardPool.AllCards.ToList();
        AutoTestAssert.True(cards.Count >= 14, $"Expected at least 14 Packmaster cards (4 starter + 10 CoreSet), got {cards.Count}");

        foreach (var card in cards)
        {
            AutoTestAssert.True(ResourceLoader.Exists(card.PortraitPath), $"Missing portrait for {card.Id}: {card.PortraitPath}");
            if (card is PackmasterCard pmCard)
                AutoTestAssert.True(ResourceLoader.Exists(pmCard.CustomPortraitPath), $"Missing big portrait for {card.Id}: {pmCard.CustomPortraitPath}");

            if (card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
                AutoTestAssert.NotNull(PackRegistry.GetForCard(card), $"Draftable card {card.Id} must belong to a registered pack");

            AutoTestAssert.True(new LocString("cards", $"{card.Id.Entry}.title").Exists(), $"Missing title loc key for {card.Id}");
            AutoTestAssert.True(card.Description.Exists(), $"Missing description loc key for {card.Id}");
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(card.Title), $"Empty title for {card.Id}");

            var desc = card.GetDescriptionForPile(PileType.None);
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(desc), $"Empty description for {card.Id}");
            AutoTestAssert.True(!desc.Contains('{') && !desc.Contains('}'), $"Unresolved SmartFormat token in {card.Id}: {desc}");

            var upgradeDesc = card.GetDescriptionForUpgradePreview();
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(upgradeDesc), $"Empty upgrade description for {card.Id}");
            AutoTestAssert.True(!upgradeDesc.Contains('{') && !upgradeDesc.Contains('}'), $"Unresolved SmartFormat token in {card.Id}+: {upgradeDesc}");
        }

        // Validate registered packs.
        AutoTestAssert.True(PackRegistry.AllPacks.Count >= 1, "At least 1 pack registered");
        var poolTypes = cards.Select(c => c.GetType()).ToHashSet();
        foreach (var pack in PackRegistry.AllPacks)
        {
            AutoTestAssert.True(pack.Title.Exists(), $"Missing pack title loc: {pack.Id}");
            AutoTestAssert.True(pack.Description.Exists(), $"Missing pack description loc: {pack.Id}");
            AutoTestAssert.True(pack.CardTypes.Count > 0, $"Pack {pack.Id} has no cards");
            foreach (var cardType in pack.CardTypes)
                AutoTestAssert.True(poolTypes.Contains(cardType), $"Pack {pack.Id} card {cardType.Name} not in PackmasterCardPool");
        }

        // Validate relics.
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

        // Validate custom powers.
        var customPowers = ModelDb.AllPowers.OfType<PackmasterPower>().ToList();
        AutoTestAssert.True(customPowers.Count >= 1, "Expected at least 1 PackmasterPower");
        foreach (var power in customPowers)
        {
            foreach (var iconPath in power.AllIconPaths)
                AutoTestAssert.True(ResourceLoader.Exists(iconPath), $"Missing power asset for {power.Id}: {iconPath}");
            AutoTestAssert.True(power.Title.Exists(), $"Missing power title loc for {power.Id}");
            AutoTestAssert.True(power.Description.Exists(), $"Missing power description loc for {power.Id}");
            AutoTestAssert.True(power.SmartDescription.Exists(), $"Missing power smartDescription loc for {power.Id}");
            var smartLoc = power.SmartDescription;
            power.DynamicVars.AddTo(smartLoc);
            smartLoc.Add("Amount", 1);
            smartLoc.Add("OnPlayer", true);
            smartLoc.Add("IsMultiplayer", false);
            smartLoc.Add("PlayerCount", 1);
            smartLoc.Add("OwnerName", "Packmaster");
            var smartText = smartLoc.GetFormattedText();
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(smartText), $"Empty smartDescription for {power.Id}");
            AutoTestAssert.True(!smartText.Contains('{') && !smartText.Contains('}'), $"Unresolved SmartFormat token in {power.Id}: {smartText}");
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
            AutoTestAssert.True(new LocString("settings_ui", key).Exists(), $"Missing settings_ui loc key: {key}");
        AutoTestAssert.True(new LocString("gameplay_ui", "PACKMASTER-PACK_CARD_TYPE").Exists(), "Missing PACKMASTER-PACK_CARD_TYPE loc key");

        return Task.CompletedTask;
    }

    private static async Task TestPackSystemAndCardPoolFiltering()
    {
        var savedAllPacks = PackmasterConfig.AllPacksMode;
        var savedPacksPerRun = PackmasterConfig.PacksPerRun;

        using var packScope = PackRegistry.OverrideForTests(
        [
            new PackDefinition("CoreSet", AlwaysInclude: true, [typeof(Flick), typeof(Sideboard)]),
            new PackDefinition("TestAlpha", AlwaysInclude: false, [typeof(Showoff)]),
            new PackDefinition("TestBeta", AlwaysInclude: false, [typeof(RippedPecs)]),
        ]);

        try
        {
            PackmasterConfig.AllPacksMode = false;
            PackmasterConfig.PacksPerRun = 2;

            var roll1 = PackRunState.RollPackIdsForSeed("SEED_DET", 2);
            var roll2 = PackRunState.RollPackIdsForSeed("SEED_DET", 2);
            AutoTestAssert.Equal(2, roll1.Count, "Rolled pack count");
            AutoTestAssert.True(roll1.SequenceEqual(roll2), "RollPackIdsForSeed is deterministic");
            AutoTestAssert.True(roll1.Contains("CoreSet"), "AlwaysInclude pack always present");

            var cardPool = ModelDb.CardPool<PackmasterCardPool>();
            var unlockState = new UnlockState(SaveManager.Instance.Progress);

            var compendiumCards = cardPool.GetUnlockedCards(unlockState, CardMultiplayerConstraint.None).ToList();
            AutoTestAssert.True(compendiumCards.Any(c => c is Flick), "Compendium includes Flick");
            AutoTestAssert.True(compendiumCards.Any(c => c is Showoff), "Compendium includes Showoff");
            AutoTestAssert.True(compendiumCards.Any(c => c is RippedPecs), "Compendium includes RippedPecs");

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
            AutoTestAssert.True(lazyActive.Contains("CoreSet"), "Lazy active packs include AlwaysInclude pack");

            PackRunState.SetActivePacksForRun(runState, ["CoreSet", "TestAlpha"]);

            var filteredCards = cardPool.GetUnlockedCards(unlockState, CardMultiplayerConstraint.SingleplayerOnly).ToList();
            AutoTestAssert.True(filteredCards.Any(c => c is PackmasterStrike), "Filtered pool includes Basic Strike");
            AutoTestAssert.True(filteredCards.Any(c => c is Flick), "Filtered pool includes active CoreSet card");
            AutoTestAssert.True(filteredCards.Any(c => c is Showoff), "Filtered pool includes active TestAlpha card");
            AutoTestAssert.True(!filteredCards.Any(c => c is RippedPecs), "Filtered pool excludes inactive TestBeta card");

            var plaqueMod = AutoTestAssert.NotNull(
                ((PackmasterCard)ModelDb.Card<Flick>()).GetTypeModifiers().FirstOrDefault(),
                "Flick type plaque modifier");
            plaqueMod.Add("Type", "Attack");
            AutoTestAssert.True(plaqueMod.GetFormattedText().Contains("Core Set"), "Type plaque contains localized pack name");
            AutoTestAssert.True(!((PackmasterCard)ModelDb.Card<PackmasterStrike>()).GetTypeModifiers().Any(), "Basic Strike has no pack type modifier");

            var rewardOptions = new CardCreationOptions([cardPool], CardCreationSource.Encounter, CardRarityOddsType.RegularEncounter);
            for (int i = 0; i < 10; i++)
            {
                var rewards = CardFactory.CreateForReward(player, 3, rewardOptions).ToList();
                AutoTestAssert.Equal(3, rewards.Count, "Reward card count");
                AutoTestAssert.True(rewards.All(r => r.Card is not RippedPecs), "Reward must not contain inactive pack card");
            }

            PackmasterConfig.AllPacksMode = true;
            var allModeCards = cardPool.GetUnlockedCards(unlockState, CardMultiplayerConstraint.SingleplayerOnly).ToList();
            AutoTestAssert.True(allModeCards.Any(c => c is RippedPecs), "AllPacksMode includes TestBeta card");
            PackmasterConfig.AllPacksMode = false;

            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, showTransition: false);
            await WaitUntilAsync(() =>
                CombatManager.Instance.IsInProgress
                && !CombatManager.Instance.PlayerActionsDisabled
                && player.PlayerCombatState is not null
                && !RunManager.Instance.ActionExecutor.IsRunning);

            var combatState = AutoTestAssert.NotNull(player.Creature.CombatState, "CombatState for synergy test");
            foreach (var starterCard in player.PlayerCombatState!.Hand.Cards.Skip(2).ToList())
                await CardPileCmd.Add(starterCard, PileType.Discard);

            var coreCard = combatState.CreateCard<Flick>(player);
            var coreCard2 = combatState.CreateCard<Sideboard>(player);
            var alphaCard = combatState.CreateCard<Showoff>(player);
            var alphaCard2 = combatState.CreateCard<Showoff>(player);
            var betaCard = combatState.CreateCard<RippedPecs>(player);

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
        var (player, _, combatState) = await StartCombatAsync("PMTEST01");

        AutoTestAssert.Equal(75, player.Creature.MaxHp, "Run player MaxHp");
        AutoTestAssert.Equal(10, player.Deck.Cards.Count, "Starter deck size");
        AutoTestAssert.True(player.Relics.Any(r => r is HandyHaversack), "Player starts with HandyHaversack");

        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");

        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "Turn-1 hand size with HandyHaversack");
        AutoTestAssert.Equal(4, pcs.Energy, "Turn-1 energy with HandyHaversack");

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

        var enemyHpBefore = enemy.CurrentHp;
        AutoTestAssert.True(rummage.TryManualPlay(enemy), "Play Rummage");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(enemyHpBefore - 10, enemy.CurrentHp, "Enemy HP after Rummage");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(strikeForDraw), "Rummage drew top card into hand");
        AutoTestAssert.Equal(0, strikeForDraw.EnergyCost.GetWithModifiers(CostModifiers.All), "Rummage reduced drawn card cost to 0");

        AutoTestAssert.True(cardistry.TryManualPlay(null), "Play Cardistry");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.Block, "Block after Cardistry");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(cardistry), "Cardistry went to discard pile");
        AutoTestAssert.True(player.Creature.HasPower<ReboundPower>(), "Cardistry applied ReboundPower");

        AutoTestAssert.True(defendForPlay.TryManualPlay(null), "Play Defend after Cardistry");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(7, player.Creature.Block, "Block after Cardistry + Defend");
        AutoTestAssert.True(pcs.DrawPile.Cards.FirstOrDefault() == defendForPlay, "Rebounded Defend is on top of Draw Pile");

        var energyBeforeFreeStrike = pcs.Energy;
        var enemyHpBeforeStrike = enemy.CurrentHp;
        AutoTestAssert.True(strikeForDraw.TryManualPlay(enemy), "Play 0-cost Rummaged Strike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeFreeStrike, pcs.Energy, "0-cost Rummaged Strike spent 0 energy");
        AutoTestAssert.Equal(enemyHpBeforeStrike - 6, enemy.CurrentHp, "Enemy HP after Strike");

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

    private static async Task TestCoreSetCardsInCombat()
    {
        // Use a 3-pack override so Synergy (2 other packs in hand), BoosterTutor, and DestinyDraw are all exercised.
        using var packScope = PackRegistry.OverrideForTests(
        [
            new PackDefinition("CoreSet", AlwaysInclude: true,
            [
                typeof(BackpackSmack), typeof(BoosterTutor), typeof(DestinyDraw), typeof(Flick),
                typeof(MayhemForm), typeof(RippedPecs), typeof(SmithingHammer), typeof(Synergize),
            ]),
            new PackDefinition("TestAlpha", AlwaysInclude: false, [typeof(Showoff)]),
            new PackDefinition("TestBeta", AlwaysInclude: false, [typeof(Sideboard)]),
        ]);

        var (player, runState, combatState) = await StartCombatAsync("PMCORE01");
        PackRunState.SetActivePacksForRun(runState, ["CoreSet", "TestAlpha", "TestBeta"]);

        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");

        // Boost enemy HP high so it survives all 10 card plays cleanly.
        await CreatureCmd.GainMaxHp(enemy, 500m);

        // Clear hand to Discard so we have full control over hand contents.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);

        // Helper: grant energy before each card test.
        async Task RefillEnergy(int target = 5)
        {
            if (pcs.Energy < target)
                await PlayerCmd.GainEnergy(target - pcs.Energy, player);
        }

        // 1. Showoff: +13 Block + DrawCardsNextTurnPower(2).
        await RefillEnergy();
        var showoff = combatState.CreateCard<Showoff>(player);
        await CardPileCmd.Add(showoff, PileType.Hand);
        AutoTestAssert.True(showoff.TryManualPlay(null), "Play Showoff");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(13, player.Creature.Block, "Showoff Block");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<DrawCardsNextTurnPower>(), "Showoff DrawCardsNextTurn");

        // 2. Sideboard: +8 Block + moves a card from Discard to top of Draw Pile.
        await RefillEnergy();
        var sideboard = combatState.CreateCard<Sideboard>(player);
        await CardPileCmd.Add(sideboard, PileType.Hand);
        int discardBefore = pcs.DiscardPile.Cards.Count;
        AutoTestAssert.True(sideboard.TryManualPlay(null), "Play Sideboard");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(21, player.Creature.Block, "Sideboard cumulative Block");
        AutoTestAssert.Equal(discardBefore, pcs.DiscardPile.Cards.Count, "Sideboard moved 1 discard card to draw and put itself in discard");

        // 3. SmithingHammer: 3 damage x 2 hits (= 6) + upgrades 2 cards in hand.
        await RefillEnergy();
        var hammer = combatState.CreateCard<SmithingHammer>(player);
        var strike1 = combatState.CreateCard<PackmasterStrike>(player);
        var strike2 = combatState.CreateCard<PackmasterStrike>(player);
        await CardPileCmd.Add(hammer, PileType.Hand);
        await CardPileCmd.Add(strike1, PileType.Hand);
        await CardPileCmd.Add(strike2, PileType.Hand);
        int hpBeforeHammer = enemy.CurrentHp;
        AutoTestAssert.True(hammer.TryManualPlay(enemy), "Play SmithingHammer");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeHammer - 6, enemy.CurrentHp, "SmithingHammer 3x2 damage");
        AutoTestAssert.True(strike1.IsUpgraded && strike2.IsUpgraded, "SmithingHammer upgraded 2 hand cards");

        // 4. Flick: 4 dmg + discard a 2-cost Showoff -> 4 dmg x 2 extra hits = 12 total dmg.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        var flick = combatState.CreateCard<Flick>(player);
        var discardFodder = combatState.CreateCard<Showoff>(player); // cost 2
        await CardPileCmd.Add(flick, PileType.Hand);
        await CardPileCmd.Add(discardFodder, PileType.Hand);
        int hpBeforeFlick = enemy.CurrentHp;
        AutoTestAssert.True(flick.TryManualPlay(enemy), "Play Flick");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeFlick - 12, enemy.CurrentHp, "Flick 4 + (4*2) damage");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(discardFodder), "Flick discarded Showoff");

        // 5. BoosterTutor: pulls 1 card from each distinct pack in Draw Pile into Hand.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        var tutor = combatState.CreateCard<BoosterTutor>(player);
        var drawAlpha1 = combatState.CreateCard<Showoff>(player);
        var drawAlpha2 = combatState.CreateCard<Showoff>(player);
        var drawBeta = combatState.CreateCard<Sideboard>(player);
        await CardPileCmd.Add(tutor, PileType.Hand);
        await CardPileCmd.Add(drawAlpha1, PileType.Draw);
        await CardPileCmd.Add(drawAlpha2, PileType.Draw);
        await CardPileCmd.Add(drawBeta, PileType.Draw);
        AutoTestAssert.True(tutor.TryManualPlay(null), "Play BoosterTutor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "BoosterTutor pulled exactly 1 card per distinct pack (Alpha + Beta)");

        // 6. DestinyDraw: adds 1 chosen 0-cost card to hand and Exhausts itself.
        await RefillEnergy();
        var destiny = combatState.CreateCard<DestinyDraw>(player);
        await CardPileCmd.Add(destiny, PileType.Hand);
        var handBeforeDestinySet = pcs.Hand.Cards.ToHashSet();
        int handBeforeDestiny = pcs.Hand.Cards.Count;
        AutoTestAssert.True(destiny.TryManualPlay(null), "Play DestinyDraw");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(destiny), "DestinyDraw exhausted");
        AutoTestAssert.Equal(handBeforeDestiny, pcs.Hand.Cards.Count, "DestinyDraw replaced itself with 1 chosen card in hand");
        var destinyAdded = AutoTestAssert.NotNull(
            pcs.Hand.Cards.FirstOrDefault(c => !handBeforeDestinySet.Contains(c)),
            "Card added by DestinyDraw");
        if (!destinyAdded.EnergyCost.CostsX && destinyAdded.EnergyCost.Canonical >= 0)
            AutoTestAssert.Equal(0, destinyAdded.EnergyCost.GetWithModifiers(CostModifiers.All), "DestinyDraw card costs 0 this turn");

        // 7. Synergy cards: keep 1 TestAlpha (Showoff) + 1 TestBeta (Sideboard) in hand so CoreSet cards have Synergy!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var synAlpha = combatState.CreateCard<Showoff>(player);
        var synBeta = combatState.CreateCard<Sideboard>(player);
        await CardPileCmd.Add(synAlpha, PileType.Hand);
        await CardPileCmd.Add(synBeta, PileType.Hand);

        // 7a. Synergize: AoE 7 dmg + Synergy 7 Block.
        await RefillEnergy();
        var synergize = combatState.CreateCard<Synergize>(player);
        await CardPileCmd.Add(synergize, PileType.Hand);
        int blockBeforeSyn = player.Creature.Block;
        int hpBeforeSyn = enemy.CurrentHp;
        AutoTestAssert.True(synergize.TryManualPlay(null), "Play Synergize with Synergy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeSyn - 7, enemy.CurrentHp, "Synergize AoE damage");
        AutoTestAssert.Equal(blockBeforeSyn + 7, player.Creature.Block, "Synergize Synergy Block");

        // 7b. RippedPecs: Synergy grants 3 Strength.
        await RefillEnergy();
        var pecs = combatState.CreateCard<RippedPecs>(player);
        await CardPileCmd.Add(pecs, PileType.Hand);
        AutoTestAssert.True(pecs.TryManualPlay(null), "Play RippedPecs with Synergy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<StrengthPower>(), "RippedPecs Synergy Strength");

        // 7c. BackpackSmack: X-cost (spend all energy = 2) with Synergy -> (8+3)*2 = 22 dmg + 2 Weak + 2 Vulnerable.
        await PlayerCmd.LoseEnergy(pcs.Energy, player);
        await PlayerCmd.GainEnergy(2, player);
        var smack = combatState.CreateCard<BackpackSmack>(player);
        await CardPileCmd.Add(smack, PileType.Hand);
        int hpBeforeSmack = enemy.CurrentHp;
        AutoTestAssert.True(smack.TryManualPlay(enemy), "Play BackpackSmack with X=2 and Synergy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeSmack - 22, enemy.CurrentHp, "BackpackSmack (8+3 str) x 2 damage");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "BackpackSmack Synergy Weak");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "BackpackSmack Synergy Vulnerable");

        // 8. MayhemForm ("Unrelenting Form"): when hand drops to <= 2 cards, draws 2 cards and gains 1 energy once per turn.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<PackmasterStrike>(player), PileType.Draw);
        await PlayerCmd.GainEnergy(3, player);
        var form = combatState.CreateCard<MayhemForm>(player);
        var filler1 = combatState.CreateCard<PackmasterDefend>(player);
        var filler2 = combatState.CreateCard<PackmasterDefend>(player);
        await CardPileCmd.Add(form, PileType.Hand);
        await CardPileCmd.Add(filler1, PileType.Hand);
        await CardPileCmd.Add(filler2, PileType.Hand);
        // Playing MayhemForm leaves 2 cards in hand -> triggers immediately: +2 draw (hand becomes 4) and +1 energy (0 -> 1)!
        AutoTestAssert.True(form.TryManualPlay(null), "Play MayhemForm");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<MayhemFormPower>(), "MayhemFormPower applied");
        AutoTestAssert.Equal(4, pcs.Hand.Cards.Count, "MayhemFormPower triggered at 2 cards in hand -> drew 2 (4 total)");
        AutoTestAssert.Equal(1, pcs.Energy, "MayhemFormPower granted 1 energy");

        // Drop hand back to <= 2 cards and play another card in the SAME turn -> MayhemFormPower must NOT trigger twice!
        foreach (var c in pcs.Hand.Cards.Skip(2).ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var secondPlay = pcs.Hand.Cards.First();
        AutoTestAssert.True(secondPlay.TryManualPlay(secondPlay.TargetType == TargetType.AnyEnemy ? enemy : null), "Play card after MayhemForm already triggered");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, pcs.Hand.Cards.Count, "MayhemFormPower did not trigger a second time in the same turn");
        AutoTestAssert.Equal(0, pcs.Energy, "MayhemFormPower did not grant energy a second time in the same turn");
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
