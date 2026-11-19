using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using Packmaster.Cards;
using Packmaster.Cards.Batter;
using Packmaster.Cards.BitingCold;
using Packmaster.Cards.CoreSet;
using Packmaster.Cards.EvenOdd;
using Packmaster.Cards.HighEnergy;
using Packmaster.Cards.Quiet;
using Packmaster.Cards.Warlock;
using Packmaster.Character;
using Packmaster.Config;
using Packmaster.Packs;
using Packmaster.Powers;
using Packmaster.Relics;

namespace Packmaster.Testing;

internal static class PackmasterAutoTestRunner
{
    private static int _unhandledErrors;
    private static ICardSelector _activeCardSelector = new AutoSlayCardSelector(new Rng(1));

    private sealed class DelegatingCardSelector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) =>
            _activeCardSelector.GetSelectedCards(options, minSelect, maxSelect);

        public CardRewardSelection GetSelectedCardReward(
            IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) =>
            _activeCardSelector.GetSelectedCardReward(options, alternatives);
    }

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
                ("PackRunStateSaveLoadPersistence", TestPackRunStateSaveLoadPersistence),
                ("NeowInteractivePackDraft", TestNeowInteractivePackDraft),
                ("CorePackmasterRelics", TestCorePackmasterRelics),
                ("StartRunAndPlayStarterCombat", TestStartRunAndPlayStarterCombat),
                ("CoreSetCardsInCombat", TestCoreSetCardsInCombat),
                ("QuietPackInCombat", TestQuietPackInCombat),
                ("BitingColdPackInCombat", TestBitingColdPackInCombat),
                ("WarlockPackInCombat", TestWarlockPackInCombat),
                ("EvenOddPackInCombat", TestEvenOddPackInCombat),
                ("HighEnergyPackInCombat", TestHighEnergyPackInCombat),
                ("BatterPackInCombat", TestBatterPackInCombat),
            ];

            foreach (var (name, run) in tests)
            {
                if (!string.IsNullOrEmpty(filter) && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                var errorsBefore = Volatile.Read(ref _unhandledErrors);
                try
                {
                    _activeCardSelector = new AutoSlayCardSelector(new Rng(1));
                    using var selectorScope = CardSelectorStack.SetTestSelector(new DelegatingCardSelector());
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

    private static async Task EndTurnAndWaitForRoundAsync(Player player, int targetRound)
    {
        var roundStarted = new TaskCompletionSource();
        void OnTurnStarted(CombatState state)
        {
            if (state.RoundNumber == targetRound && state.CurrentSide == CombatSide.Player)
                roundStarted.TrySetResult();
        }
        CombatManager.Instance.TurnStarted += OnTurnStarted;
        try
        {
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
                new EndPlayerTurnAction(player, player.PlayerCombatState!.TurnNumber));
            await roundStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await WaitUntilAsync(() => !CombatManager.Instance.PlayerActionsDisabled && !RunManager.Instance.ActionExecutor.IsRunning);
        }
        finally
        {
            CombatManager.Instance.TurnStarted -= OnTurnStarted;
        }
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
        AutoTestAssert.True(new LocString("gameplay_ui", "PACKMASTER-DRAFT_PAGE.description").Exists(), "Missing PACKMASTER-DRAFT_PAGE.description loc key");
        AutoTestAssert.True(new LocString("gameplay_ui", "PACKMASTER-DRAFT_OPTION.description").Exists(), "Missing PACKMASTER-DRAFT_OPTION.description loc key");
        AutoTestAssert.True(new LocString("relics", "PACKMASTER-PM_BOOSTER_PACK.selectionScreenPrompt").Exists(), "Missing PACKMASTER-PM_BOOSTER_PACK.selectionScreenPrompt loc key");
        AutoTestAssert.True(new LocString("relics", "PACKMASTER-PM_COLLECTION.selectionScreenPrompt").Exists(), "Missing PACKMASTER-PM_COLLECTION.selectionScreenPrompt loc key");

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

        await EndTurnAndWaitForRoundAsync(player, 2);

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

    private static async Task TestQuietPackInCombat()
    {
        var (player, _, combatState) = await StartCombatAsync("PMQUIET1");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");
        await CreatureCmd.GainMaxHp(enemy, 500m);

        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);

        async Task RefillEnergy(int target = 6)
        {
            if (pcs.Energy < target)
                await PlayerCmd.GainEnergy(target - pcs.Energy, player);
        }

        // 1. IronShield: +11 Block + adds 1 Weight to hand; Oppressor: 11 dmg + discards Weight -> Weight exhausts!
        await RefillEnergy();
        var ironShield = combatState.CreateCard<IronShield>(player);
        await CardPileCmd.Add(ironShield, PileType.Hand);
        AutoTestAssert.True(ironShield.TryManualPlay(null), "Play IronShield");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(11, player.Creature.Block, "IronShield Block");
        var weight = AutoTestAssert.NotNull(pcs.Hand.Cards.FirstOrDefault(c => c is Weight), "Weight added to hand by IronShield");

        var oppressor = combatState.CreateCard<Oppressor>(player);
        await CardPileCmd.Add(oppressor, PileType.Hand);
        int hpBeforeOppressor = enemy.CurrentHp;
        AutoTestAssert.True(oppressor.TryManualPlay(enemy), "Play Oppressor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeOppressor - 11, enemy.CurrentHp, "Oppressor 11 damage");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(weight), "Discarded Weight exhausted itself");

        // 2. Lie: enemy loses 4 Strength this turn (PiercingWailPower + StrengthPower -4) + adds Weight.
        await RefillEnergy();
        var lie = combatState.CreateCard<Lie>(player);
        await CardPileCmd.Add(lie, PileType.Hand);
        AutoTestAssert.True(lie.TryManualPlay(enemy), "Play Lie");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, enemy.GetPowerAmount<PiercingWailPower>(), "Lie applied PiercingWailPower(4)");
        AutoTestAssert.Equal(-4, enemy.GetPowerAmount<StrengthPower>(), "Lie reduced enemy Strength by 4 this turn");
        AutoTestAssert.True(pcs.Hand.Cards.Any(c => c is Weight), "Lie added Weight to hand");

        // 3. Crunch & Shine on-draw triggers + Frontflip discarding unplayable drawn Weight!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);

        var targetForShine = combatState.CreateCard<PackmasterStrike>(player);
        await CardPileCmd.Add(targetForShine, PileType.Hand);

        var drawExtra1 = combatState.CreateCard<PackmasterDefend>(player);
        var drawExtra2 = combatState.CreateCard<PackmasterDefend>(player);
        var weightInDraw = combatState.CreateCard<Weight>(player);
        var crunch = combatState.CreateCard<Crunch>(player);
        var shine = combatState.CreateCard<Shine>(player);

        // Top of draw pile order (last added to Top is drawn first): shine, crunch, drawExtra1, drawExtra2, weightInDraw
        await CardPileCmd.Add(drawExtra2, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(drawExtra1, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(crunch, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Add(shine, PileType.Draw, CardPilePosition.Top);

        // Draw Shine -> upgrades targetForShine!
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1m, player);
        AutoTestAssert.True(targetForShine.IsUpgraded, "Drawing Shine upgraded another card in hand");

        // Draw Crunch -> automatically draws 2 more cards (drawExtra1, drawExtra2)!
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1m, player);
        AutoTestAssert.True(pcs.Hand.Cards.Contains(drawExtra1) && pcs.Hand.Cards.Contains(drawExtra2), "Drawing Crunch drew 2 extra cards");

        // 4. Frontflip: +13 Block, draws 3 cards, auto-discards unplayable Weight (which then exhausts)!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        await CardPileCmd.Add(weightInDraw, PileType.Draw, CardPilePosition.Top);
        var frontflip = combatState.CreateCard<Frontflip>(player);
        await CardPileCmd.Add(frontflip, PileType.Hand);
        int blockBeforeFlip = player.Creature.Block;
        AutoTestAssert.True(frontflip.TryManualPlay(null), "Play Frontflip");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBeforeFlip + 13, player.Creature.Block, "Frontflip Block");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(weightInDraw), "Frontflip discarded drawn unplayable Weight (which exhausted)");

        // 5. GlaiveThrow: 7 dmg + draw 2 + discard 2 (net -1 card since GlaiveThrow leaves hand).
        await RefillEnergy();
        var glaive = combatState.CreateCard<GlaiveThrow>(player);
        await CardPileCmd.Add(glaive, PileType.Hand);
        int hpBeforeGlaive = enemy.CurrentHp;
        int handBeforeGlaive = pcs.Hand.Cards.Count;
        int discardBeforeGlaive = pcs.DiscardPile.Cards.Count;
        AutoTestAssert.True(glaive.TryManualPlay(enemy), "Play GlaiveThrow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeGlaive - 7, enemy.CurrentHp, "GlaiveThrow 7 damage");
        AutoTestAssert.Equal(handBeforeGlaive - 1, pcs.Hand.Cards.Count, "GlaiveThrow net hand size (play -1, draw +2, discard -2)");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Count >= discardBeforeGlaive + 2, "GlaiveThrow discarded cards");

        // 6. Wisdom: +1 Energy, discard/draw, Exhaust.
        await RefillEnergy(2);
        var wisdom = combatState.CreateCard<Wisdom>(player);
        await CardPileCmd.Add(wisdom, PileType.Hand);
        int energyBeforeWisdom = pcs.Energy;
        AutoTestAssert.True(wisdom.TryManualPlay(null), "Play Wisdom");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBeforeWisdom + 1, pcs.Energy, "Wisdom gained 1 Energy");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(wisdom), "Wisdom exhausted");

        // 7. Flail (13x2 + 2 Weights) + HammerThrow (3 AoE dmg per retained card at end of turn).
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        var flail = combatState.CreateCard<Flail>(player);
        var hammerThrow = combatState.CreateCard<HammerThrow>(player);
        await CardPileCmd.Add(flail, PileType.Hand);
        await CardPileCmd.Add(hammerThrow, PileType.Hand);

        int hpBeforeFlail = enemy.CurrentHp;
        AutoTestAssert.True(flail.TryManualPlay(enemy), "Play Flail");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeFlail - 26, enemy.CurrentHp, "Flail 13x2 damage");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count(c => c is Weight), "Flail added 2 Weights to hand");

        AutoTestAssert.True(hammerThrow.TryManualPlay(null), "Play HammerThrow");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<HammerThrowPower>(), "HammerThrowPower(3) applied");

        // Put Shine+ on TOP of draw pile (above 4 fresh Strikes) so it is drawn FIRST on Turn 2 start-of-turn hand draw!
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var turn2Strikes = Enumerable.Range(0, 4).Select(_ => combatState.CreateCard<PackmasterStrike>(player)).ToList();
        foreach (var s in turn2Strikes)
            await CardPileCmd.Add(s, PileType.Draw, CardPilePosition.Top);
        var shinePlus = combatState.CreateCard<Shine>(player);
        CardCmd.Upgrade(shinePlus);
        await CardPileCmd.Add(shinePlus, PileType.Draw, CardPilePosition.Top);

        // End turn with 2 Retained Weights in hand -> HammerThrow deals 3 x 2 = 6 AoE damage!
        int hpBeforeEndTurn = enemy.CurrentHp;
        await EndTurnAndWaitForRoundAsync(player, 2);

        AutoTestAssert.Equal(hpBeforeEndTurn - 6, enemy.CurrentHp, "HammerThrow dealt 3x2=6 damage for 2 retained Weights");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<StrengthPower>(), "Lie temporary Strength loss restored on Turn 2");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count(c => c is Weight), "Retained Weights stayed in hand and did not exhaust");
        AutoTestAssert.True(turn2Strikes.All(s => s.IsUpgraded), "Shine+ drawn as card #1 on Turn 2 upgraded all other drawn cards");
    }

    private static async Task TestBitingColdPackInCombat()
    {
        var (player, _, combatState) = await StartCombatAsync("PMCOLD01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");

        await CreatureCmd.GainMaxHp(enemy, 800m);
        await CreatureCmd.GainMaxHp(player.Creature, 200m);

        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);

        async Task RefillEnergy(int target = 6)
        {
            if (pcs.Energy < target)
                await PlayerCmd.GainEnergy(target - pcs.Energy, player);
        }

        // 1. Subdue before any Attack -> 6 Block.
        await RefillEnergy();
        var subdue1 = combatState.CreateCard<Subdue>(player);
        await CardPileCmd.Add(subdue1, PileType.Hand);
        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(subdue1.TryManualPlay(null), "Play Subdue before Attack");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 6, player.Creature.Block, "Subdue gives single 6 Block when last card was not Attack");

        // 2. GrowingAffliction (11 dmg, goes to Discard) -> Subdue after Attack -> 12 Block!
        var growing = combatState.CreateCard<GrowingAffliction>(player);
        var subdue2 = combatState.CreateCard<Subdue>(player);
        await CardPileCmd.Add(growing, PileType.Hand);
        await CardPileCmd.Add(subdue2, PileType.Hand);
        int hpBeforeGrowing = enemy.CurrentHp;
        AutoTestAssert.True(growing.TryManualPlay(enemy), "Play GrowingAffliction");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeGrowing - 11, enemy.CurrentHp, "GrowingAffliction 11 damage");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(growing), "GrowingAffliction in Discard Pile");
        growing.EnergyCost.SetThisCombat(0);
        growing.EnergyCost.SetThisTurn(0);

        blockBefore = player.Creature.Block;
        AutoTestAssert.True(subdue2.TryManualPlay(null), "Play Subdue after Attack");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 12, player.Creature.Block, "Subdue gives 6+6=12 Block when last card was Attack");

        // 3. FrostedEdge (+1 temp Strength per Frostbite) + Refrigerate (11 Frostbite, Exhaust, pulls GrowingAffliction back at cost 1!).
        await RefillEnergy();
        var frostedEdge = combatState.CreateCard<FrostedEdge>(player);
        var refrigerate = combatState.CreateCard<Refrigerate>(player);
        await CardPileCmd.Add(frostedEdge, PileType.Hand);
        await CardPileCmd.Add(refrigerate, PileType.Hand);
        AutoTestAssert.True(frostedEdge.TryManualPlay(null), "Play FrostedEdge");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<FrostedEdgePower>(), "FrostedEdgePower(1) applied");

        AutoTestAssert.True(refrigerate.TryManualPlay(enemy), "Play Refrigerate");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(11, enemy.GetPowerAmount<FrostbitePower>(), "Refrigerate applied 11 Frostbite");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(refrigerate), "Refrigerate exhausted");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "FrostedEdge granted +1 temp Strength on Frostbite");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(growing), "GrowingAffliction returned from Discard to Hand on debuff");
        AutoTestAssert.Equal(1, growing.EnergyCost.GetWithModifiers(CostModifiers.All), "GrowingAffliction cost reset to 1");

        // 4. Refrigerate+ when enemy already has 11 Frostbite -> applies 11 more (=22) and immediately loses 22 HP without halving!
        await RefillEnergy();
        var refrigeratePlus = combatState.CreateCard<Refrigerate>(player);
        CardCmd.Upgrade(refrigeratePlus);
        await CardPileCmd.Add(refrigeratePlus, PileType.Hand);
        int hpBeforeRefPlus = enemy.CurrentHp;
        AutoTestAssert.True(refrigeratePlus.TryManualPlay(enemy), "Play Refrigerate+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(22, enemy.GetPowerAmount<FrostbitePower>(), "Refrigerate+ stacked Frostbite to 22 without halving");
        AutoTestAssert.Equal(hpBeforeRefPlus - 22, enemy.CurrentHp, "Refrigerate+ triggered 22 HP loss");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "FrostedEdge granted second +1 temp Strength");

        // 5. InsultToInjury: enemy has Frostbite + Weak (2 unique debuffs) -> 3 hits of (5 + 2 Str = 7) = 21 dmg, plus +3 Frostbite (on attack)!
        await RefillEnergy();
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), enemy, 2m, player.Creature, null);
        var insult = combatState.CreateCard<InsultToInjury>(player);
        await CardPileCmd.Add(insult, PileType.Hand);
        int hpBeforeInsult = enemy.CurrentHp;
        int frostbiteBeforeInsult = enemy.GetPowerAmount<FrostbitePower>();
        AutoTestAssert.True(insult.TryManualPlay(enemy), "Play InsultToInjury");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        // Hits: 1st hit dmg = 5+2=7 (+1 Frostbite -> +1 Str), 2nd hit dmg = 5+3=8 (+1 Frostbite -> +1 Str), 3rd hit dmg = 5+4=9 (+1 Frostbite -> +1 Str).
        // Total = 7 + 8 + 9 = 24 dmg, Frostbite +3!
        AutoTestAssert.Equal(frostbiteBeforeInsult + 3, enemy.GetPowerAmount<FrostbitePower>(), "Frostbite increased by +1 on each of 3 hits");
        AutoTestAssert.Equal(hpBeforeInsult - 24, enemy.CurrentHp, "InsultToInjury hit 3 times with scaling FrostedEdge Strength");

        // 6. Frostburn: applies 18 Frostbite to ALL enemies + adds Burn to Discard Pile.
        await RefillEnergy();
        int frostBeforeBurn = enemy.GetPowerAmount<FrostbitePower>();
        var frostburn = combatState.CreateCard<Frostburn>(player);
        await CardPileCmd.Add(frostburn, PileType.Hand);
        AutoTestAssert.True(frostburn.TryManualPlay(null), "Play Frostburn");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(frostBeforeBurn + 18, enemy.GetPowerAmount<FrostbitePower>(), "Frostburn applied 18 Frostbite");
        AutoTestAssert.True(pcs.DiscardPile.Cards.Any(c => c is MegaCrit.Sts2.Core.Models.Cards.Burn), "Frostburn added Burn to Discard Pile");

        // 7. RelentlessHail: 2 hits first play (dealing damage + triggering Frostbite +1 per hit), increases hits to 3 for next play.
        await RefillEnergy();
        var hail = combatState.CreateCard<RelentlessHail>(player);
        await CardPileCmd.Add(hail, PileType.Hand);
        AutoTestAssert.Equal(2, hail.DynamicVars.Repeat.IntValue, "RelentlessHail starts at 2 hits");
        int hpBeforeHail = enemy.CurrentHp;
        int strBeforeHail = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(hail.TryManualPlay(null), "Play RelentlessHail");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        // Hit 1: 6 + strBeforeHail, then Frostbite +1 -> FrostedEdge +1 Str; Hit 2: 6 + strBeforeHail + 1.
        int expectedHailDmg = (6 + strBeforeHail) + (6 + strBeforeHail + 1);
        AutoTestAssert.Equal(hpBeforeHail - expectedHailDmg, enemy.CurrentHp, "RelentlessHail dealt 2 hits of AoE damage");
        AutoTestAssert.Equal(3, hail.DynamicVars.Repeat.IntValue, "RelentlessHail increased to 3 hits after play");

        // 8. FrigidBody: +9 Block + applies Frostbite equal to blocked attack damage.
        await RefillEnergy();
        var frigid = combatState.CreateCard<FrigidBody>(player);
        await CardPileCmd.Add(frigid, PileType.Hand);
        int blockBeforeFrigid = player.Creature.Block;
        AutoTestAssert.True(frigid.TryManualPlay(null), "Play FrigidBody");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBeforeFrigid + 9, player.Creature.Block, "FrigidBody +9 Block");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<FrigidBodyPower>(), "FrigidBodyPower(1) applied");

        // Enemy currently has WeakPower, so an 8-damage powered attack deals floor(8 * 0.75) = 6 blocked damage.
        int frostBeforeBlockedHit = enemy.GetPowerAmount<FrostbitePower>();
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 8m, ValueProp.Move, dealer: enemy, cardSource: null, cardPlay: null);
        AutoTestAssert.Equal(frostBeforeBlockedHit + 6, enemy.GetPowerAmount<FrostbitePower>(), "FrigidBody applied Frostbite equal to 6 blocked damage");

        // 9. SnowedIn (+11 Frostbite, Frostbite not halved this turn) + IceShatter (9 dmg on HP loss during enemy turn).
        await RefillEnergy(10);
        var snowedIn = combatState.CreateCard<SnowedIn>(player);
        var iceShatter = combatState.CreateCard<IceShatter>(player);
        await CardPileCmd.Add(snowedIn, PileType.Hand);
        await CardPileCmd.Add(iceShatter, PileType.Hand);
        AutoTestAssert.True(snowedIn.TryManualPlay(enemy), "Play SnowedIn");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<SnowedInPower>(), "SnowedInPower(1) applied");

        AutoTestAssert.True(iceShatter.TryManualPlay(enemy), "Play IceShatter");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(9, enemy.GetPowerAmount<IceShatterPower>(), "IceShatterPower(9) applied");

        // Clear player block before ending Turn 1 so enemy's Turn-1 attack doesn't add extra FrigidBody Frostbite/FrostedEdge Strength,
        // while keeping FrigidBodyPower on the player to verify it expires after the enemy turn.
        if (player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), player.Creature, player.Creature.Block, null);

        // End Turn 1 -> Enemy turn: Frostbite triggers HP loss (not halved due to SnowedIn!) AND IceShatter triggers once for 9 dmg!
        int frostAtEndTurn1 = enemy.GetPowerAmount<FrostbitePower>();
        int enemyHpAtEndTurn1 = enemy.CurrentHp;
        await EndTurnAndWaitForRoundAsync(player, 2);

        AutoTestAssert.Equal(frostAtEndTurn1, enemy.GetPowerAmount<FrostbitePower>(), "SnowedIn prevented Frostbite from halving");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<SnowedInPower>(), "SnowedInPower expired after enemy turn");
        AutoTestAssert.Equal(0, enemy.GetPowerAmount<IceShatterPower>(), "IceShatterPower expired after enemy turn");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<FrigidBodyPower>(), "FrigidBodyPower expired after enemy turn");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<StrengthPower>(), "FrostedEdge temporary Strength expired on Turn 2");
        AutoTestAssert.Equal(
            enemyHpAtEndTurn1 - frostAtEndTurn1 - 9,
            enemy.CurrentHp,
            "Enemy lost exact Frostbite + single IceShatter (9) HP during enemy turn without re-entrancy");

        // End Turn 2 -> Enemy turn without SnowedIn: Frostbite triggers AND halves (ceil halved -> floor remaining: amount - (amount+1)/2)!
        int expectedAfterHalve = frostAtEndTurn1 - (frostAtEndTurn1 + 1) / 2;
        await EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(expectedAfterHalve, enemy.GetPowerAmount<FrostbitePower>(), "Frostbite halved on Turn 2 enemy turn without SnowedIn");
    }

    private static async Task TestWarlockPackInCombat()
    {
        var (player, _, combatState) = await StartCombatAsync("PMWARL01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");

        await CreatureCmd.GainMaxHp(enemy, 800m);

        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);

        async Task RefillEnergy(int target = 8)
        {
            if (pcs.Energy < target)
                await PlayerCmd.GainEnergy(target - pcs.Energy, player);
        }

        // 1. SoulShear (8 dmg + shuffles 1 Imp into draw pile) + Imp cast-on-draw + scaling across Imps!
        await RefillEnergy();
        var drawAfterImp = combatState.CreateCard<PackmasterStrike>(player);
        await CardPileCmd.Add(drawAfterImp, PileType.Draw, CardPilePosition.Top);
        var soulShear = combatState.CreateCard<SoulShear>(player);
        await CardPileCmd.Add(soulShear, PileType.Hand);
        int hpBeforeShear = enemy.CurrentHp;
        AutoTestAssert.True(soulShear.TryManualPlay(enemy), "Play SoulShear");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeShear - 8, enemy.CurrentHp, "SoulShear dealt 8 damage");
        var imp1 = AutoTestAssert.NotNull(pcs.DrawPile.Cards.OfType<Imp>().FirstOrDefault(), "SoulShear shuffled Imp into Draw Pile");

        // Move Imp to top of draw pile above drawAfterImp and draw it.
        await CardPileCmd.Add(imp1, PileType.Draw, CardPilePosition.Top);
        int hpBeforeImp1 = enemy.CurrentHp;
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1m, player);
        AutoTestAssert.Equal(hpBeforeImp1 - 3, enemy.CurrentHp, "First Imp dealt 3 damage on draw");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(imp1), "Imp exhausted itself on draw");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(drawAfterImp), "Imp drew 1 replacement card");

        // Second Imp (newly created!) should start scaled to 4 damage via AfterCardEnteredCombat!
        var imp2 = combatState.CreateCard<Imp>(player);
        await CardPileCmd.AddGeneratedCardToCombat(imp2, PileType.Draw, player, CardPilePosition.Top);
        AutoTestAssert.Equal(4, imp2.DynamicVars["ImpDamage"].IntValue, "Newly created Imp synced to 4 damage on entering combat");
        int hpBeforeImp2 = enemy.CurrentHp;
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1m, player);
        AutoTestAssert.Equal(hpBeforeImp2 - 4, enemy.CurrentHp, "Second Imp dealt scaled 4 damage");

        // 2. MalchezaarsImp + MalchezaarsImp+ + Soulfire exhausting HighPriestessJeklik!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy(10);

        var malImp = combatState.CreateCard<MalchezaarsImp>(player);
        var malImpPlus = combatState.CreateCard<MalchezaarsImp>(player);
        CardCmd.Upgrade(malImpPlus);
        await CardPileCmd.Add(malImp, PileType.Hand);
        await CardPileCmd.Add(malImpPlus, PileType.Hand);
        AutoTestAssert.True(malImp.TryManualPlay(null), "Play MalchezaarsImp");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(malImpPlus.TryManualPlay(null), "Play MalchezaarsImp+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<MalchezaarsImpPower>(), "MalchezaarsImpPower(1)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<MalchezaarsImpUpgradedPower>(), "MalchezaarsImpUpgradedPower(1)");

        var jeklik = combatState.CreateCard<HighPriestessJeklik>(player);
        var soulfire = combatState.CreateCard<Soulfire>(player);
        await CardPileCmd.Add(jeklik, PileType.Hand);
        await CardPileCmd.Add(soulfire, PileType.Hand);
        int impsInDiscardBefore = pcs.DiscardPile.Cards.Count(c => c is Imp);
        int impsInDrawBefore = pcs.DrawPile.Cards.Count(c => c is Imp);
        int hpBeforeSoulfire = enemy.CurrentHp;
        AutoTestAssert.True(soulfire.TryManualPlay(enemy), "Play Soulfire");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeSoulfire - 9, enemy.CurrentHp, "Soulfire dealt 9 damage");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(jeklik), "Soulfire exhausted Jeklik");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count(c => c is HighPriestessJeklik), "Exhausting Jeklik added 2 copies to hand");
        AutoTestAssert.Equal(impsInDiscardBefore + 1, pcs.DiscardPile.Cards.Count(c => c is Imp), "MalchezaarsImpPower added 1 Imp to Discard");
        AutoTestAssert.Equal(impsInDrawBefore + 1, pcs.DrawPile.Cards.Count(c => c is Imp), "MalchezaarsImpUpgradedPower shuffled 1 Imp into Draw");

        // Verify exhausting an Imp does NOT trigger MalchezaarsImp powers (no infinite loop!).
        var dummyImp = combatState.CreateCard<Imp>(player);
        await CardPileCmd.AddGeneratedCardToCombat(dummyImp, PileType.Discard, player);
        int impsInDiscardBeforeDummy = pcs.DiscardPile.Cards.Count(c => c is Imp) - 1;
        int impsInDrawBeforeDummy = pcs.DrawPile.Cards.Count(c => c is Imp);
        await CardCmd.Exhaust(new ThrowingPlayerChoiceContext(), dummyImp);
        AutoTestAssert.Equal(impsInDiscardBeforeDummy, pcs.DiscardPile.Cards.Count(c => c is Imp), "Exhausting Imp did not trigger MalchezaarsImpPower");
        AutoTestAssert.Equal(impsInDrawBeforeDummy, pcs.DrawPile.Cards.Count(c => c is Imp), "Exhausting Imp did not trigger MalchezaarsImpUpgradedPower");

        // Remove MalchezaarsImp powers so remaining tests don't spawn extra Imps in draw/discard.
        await PowerCmd.Remove<MalchezaarsImpPower>(player.Creature);
        await PowerCmd.Remove<MalchezaarsImpUpgradedPower>(player.Creature);

        // 2b. Soulfire+ (upgraded CardSelectCmd.FromHand branch): deals 11 damage and exhausts 1 chosen card.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        var soulfirePlus = combatState.CreateCard<Soulfire>(player);
        CardCmd.Upgrade(soulfirePlus);
        var exhaustTarget = combatState.CreateCard<PackmasterStrike>(player);
        await CardPileCmd.Add(soulfirePlus, PileType.Hand);
        await CardPileCmd.Add(exhaustTarget, PileType.Hand);
        int hpBeforeSoulfirePlus = enemy.CurrentHp;
        AutoTestAssert.True(soulfirePlus.TryManualPlay(enemy), "Play Soulfire+");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeSoulfirePlus - 11, enemy.CurrentHp, "Soulfire+ dealt 11 damage");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(exhaustTarget), "Soulfire+ exhausted chosen card");

        // 3. Doomguard (21 dmg + snapshot-picks up to 2 cards): with ONLY 1 Jeklik in hand, leaves exact 2 Jekliks (never re-exhausts new copy!).
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        var soloJeklik = combatState.CreateCard<HighPriestessJeklik>(player);
        var doomguardSolo = combatState.CreateCard<Doomguard>(player);
        await CardPileCmd.Add(soloJeklik, PileType.Hand);
        await CardPileCmd.Add(doomguardSolo, PileType.Hand);
        int hpBeforeDoomSolo = enemy.CurrentHp;
        AutoTestAssert.True(doomguardSolo.TryManualPlay(enemy), "Play Doomguard with solo Jeklik");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBeforeDoomSolo - 21, enemy.CurrentHp, "Doomguard dealt 21 damage");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count(c => c is HighPriestessJeklik), "Doomguard snapshot-pick did not re-exhaust Jeklik copies");

        // 3b. Doomguard exhausting HandOfGulDan + fodder -> draws exact 3 cards into hand.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<PackmasterStrike>(player), PileType.Draw, CardPilePosition.Top);

        await RefillEnergy();
        var handOfGulDan = combatState.CreateCard<HandOfGulDan>(player);
        var fodder = combatState.CreateCard<PackmasterDefend>(player);
        var doomguard = combatState.CreateCard<Doomguard>(player);
        await CardPileCmd.Add(handOfGulDan, PileType.Hand);
        await CardPileCmd.Add(fodder, PileType.Hand);
        await CardPileCmd.Add(doomguard, PileType.Hand);
        AutoTestAssert.True(doomguard.TryManualPlay(enemy), "Play Doomguard with HandOfGulDan");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(handOfGulDan), "Doomguard exhausted HandOfGulDan");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(fodder), "Doomguard exhausted fodder");
        AutoTestAssert.Equal(3, pcs.Hand.Cards.Count, "Exhausting HandOfGulDan drew exact 3 cards without Doomguard exhausting them");

        // 4. SeedsOfDestruction (Choose Draw 3 Imps or Discard 4 Imps).
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        int drawImpsBefore = pcs.DrawPile.Cards.Count(c => c is Imp);
        int discardImpsBefore = pcs.DiscardPile.Cards.Count(c => c is Imp);
        var seeds = combatState.CreateCard<SeedsOfDestruction>(player);
        await CardPileCmd.Add(seeds, PileType.Hand);
        AutoTestAssert.True(seeds.TryManualPlay(null), "Play SeedsOfDestruction");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        bool addedDraw = pcs.DrawPile.Cards.Count(c => c is Imp) == drawImpsBefore + 3;
        bool addedDiscard = pcs.DiscardPile.Cards.Count(c => c is Imp) == discardImpsBefore + 4;
        AutoTestAssert.True(addedDraw || addedDiscard, "SeedsOfDestruction added either 3 Imps to Draw or 4 Imps to Discard");

        // 5. AranasiBroodmother: +5 Block, shuffles itself into Draw Pile; drawing it grants +4 Block.
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        var aranasi = combatState.CreateCard<AranasiBroodmother>(player);
        await CardPileCmd.Add(aranasi, PileType.Hand);
        int blockBeforeAranasi = player.Creature.Block;
        AutoTestAssert.True(aranasi.TryManualPlay(null), "Play AranasiBroodmother");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBeforeAranasi + 5, player.Creature.Block, "AranasiBroodmother +5 Block on play");
        AutoTestAssert.True(pcs.DrawPile.Cards.Contains(aranasi), "AranasiBroodmother shuffled itself into Draw Pile");

        int blockBeforeDrawAranasi = player.Creature.Block;
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1m, player);
        AutoTestAssert.Equal(blockBeforeDrawAranasi + 4, player.Creature.Block, "Drawing AranasiBroodmother granted +4 Block");

        // 6. TheSoularium: draws 3 cards (including HandOfGulDan + HighPriestessJeklik), gives them Ethereal, exhausts itself.
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        // Put 8 Strikes under the 3 Soularium targets so Turn 2 draws cleanly without hitting Imps.
        for (int i = 0; i < 8; i++)
            await CardPileCmd.Add(combatState.CreateCard<PackmasterStrike>(player), PileType.Draw, CardPilePosition.Top);

        var ethStrike = combatState.CreateCard<PackmasterStrike>(player);
        var ethGulDan = combatState.CreateCard<HandOfGulDan>(player);
        var ethJeklik = combatState.CreateCard<HighPriestessJeklik>(player);
        CardModel[] soulTargets = [ethStrike, ethGulDan, ethJeklik];
        foreach (var s in soulTargets)
            await CardPileCmd.Add(s, PileType.Draw, CardPilePosition.Top);

        var soularium = combatState.CreateCard<TheSoularium>(player);
        await CardPileCmd.Add(soularium, PileType.Hand);
        AutoTestAssert.True(soularium.TryManualPlay(null), "Play TheSoularium");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(soularium), "TheSoularium exhausted itself");
        AutoTestAssert.True(soulTargets.All(c => pcs.Hand.Cards.Contains(c) && c.Keywords.Contains(CardKeyword.Ethereal)), "TheSoularium drew 3 cards and gave them Ethereal");

        // End Turn 1 with Ethereal HandOfGulDan + HighPriestessJeklik in hand -> verify clean Turn 2 (no infinite Ethereal loop, no leaked hand cards!).
        int jeklikCopiesBeforeEnd = pcs.AllCards.Count(c => c is HighPriestessJeklik && c.Pile?.Type != PileType.Exhaust);
        await EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(ethStrike), "Ethereal Strike exhausted at end of Turn 1");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(ethGulDan), "Ethereal HandOfGulDan exhausted at end of Turn 1");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(ethJeklik), "Ethereal HighPriestessJeklik exhausted at end of Turn 1");
        AutoTestAssert.Equal(5, pcs.Hand.Cards.Count, "Turn 2 starts with clean 5-card hand (no Ethereal exhaust leaks)");
        AutoTestAssert.Equal(
            jeklikCopiesBeforeEnd + 1,
            pcs.AllCards.Count(c => c is HighPriestessJeklik && c.Pile?.Type != PileType.Exhaust),
            "Ethereal-exhausted Jeklik replaced itself (-1 exhausted, +2 clones in Discard = net +1)");

        // 7. RunedMithrilRod+: Retained across turns, hits 10th draw on start-of-turn opening hand draw -> discounts ALL opening-hand >0-cost cards!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        for (int i = 0; i < 15; i++)
            await CardPileCmd.Add(combatState.CreateCard<PackmasterStrike>(player), PileType.Draw, CardPilePosition.Top);

        var rod = combatState.CreateCard<RunedMithrilRod>(player);
        CardCmd.Upgrade(rod);
        await CardPileCmd.Add(rod, PileType.Hand);

        // Draw 9 cards during Turn 2 (leaving rod at 9/10 drawn).
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 5m, player);
        foreach (var c in pcs.Hand.Cards.Where(c => c != rod).ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 4m, player);
        AutoTestAssert.Equal(9, rod.DynamicVars["Drawn"].IntValue, "RunedMithrilRod+ at 9/10 draws before Turn 2 end");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(rod), "RunedMithrilRod+ still in hand at 9/10");

        // Prepare Turn 3 draw pile: 4 one-cost Strikes + 1 zero-cost Jeklik so Turn 3 opening hand has both 1-cost and 0-cost cards.
        foreach (var c in pcs.DrawPile.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var turn3Strikes = Enumerable.Range(0, 4).Select(_ => combatState.CreateCard<PackmasterStrike>(player)).ToList();
        var turn3ZeroCost = combatState.CreateCard<HighPriestessJeklik>(player);
        await CardPileCmd.Add(turn3ZeroCost, PileType.Draw, CardPilePosition.Top);
        foreach (var s in turn3Strikes)
            await CardPileCmd.Add(s, PileType.Draw, CardPilePosition.Top);

        // End Turn 2 -> Rod Retains, Turn 3 opening draw hits 10th draw on card #1, defers to AfterPlayerTurnStart -> discounts ALL 4 Strikes!
        await EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(rod), "RunedMithrilRod+ discarded itself after Turn-3 opening hand draw");
        AutoTestAssert.Equal(0, rod.DynamicVars["Drawn"].IntValue, "RunedMithrilRod+ reset Drawn counter to 0");
        AutoTestAssert.True(turn3Strikes.All(s => s.EnergyCost.GetWithModifiers(CostModifiers.All) == 0), "RunedMithrilRod+ discounted ALL 4 opening-hand Strikes to 0");
        AutoTestAssert.Equal(0, turn3ZeroCost.EnergyCost.GetWithModifiers(CostModifiers.All), "0-cost card stayed at 0 cost");
    }

    private static async Task TestEvenOddPackInCombat()
    {
        var (player, _, combatState) = await StartCombatAsync("PMEVEN01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");

        await CreatureCmd.GainMaxHp(enemy, 800m);
        await CreatureCmd.GainMaxHp(player.Creature, 200m);

        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);

        async Task RefillEnergy(int target = 15)
        {
            if (pcs.Energy < target)
                await PlayerCmd.GainEnergy(target - pcs.Energy, player);
        }

        // === TURN 1 ===
        await RefillEnergy();
        AutoTestAssert.True(!ModelDb.Card<Flourish>().CanBeGeneratedInCombat, "Flourish cannot be randomly generated in combat");

        // Card #1 (First / Odd): Battlecry draws 2 + grants +2 Strength.
        var battlecry = combatState.CreateCard<Battlecry>(player);
        await CardPileCmd.Add(battlecry, PileType.Hand);
        AutoTestAssert.True(battlecry.TryManualPlay(null), "Play Battlecry (#1 First)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "Battlecry as First card granted +2 Strength");
        AutoTestAssert.Equal(2, pcs.Hand.Cards.Count, "Battlecry drew 2 cards");

        // Card #2 (Even): GammaWard -> +4 Block + GammaWardPower(12).
        var gamma1 = combatState.CreateCard<GammaWard>(player);
        await CardPileCmd.Add(gamma1, PileType.Hand);
        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(gamma1.TryManualPlay(null), "Play GammaWard (#2 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 4, player.Creature.Block, "GammaWard +4 Block");
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<GammaWardPower>(), "GammaWard as Even card applied GammaWardPower(12)");

        // Card #3 (Odd): SwordAndBoard acts as Skill -> +8 Block, consumes GammaWardPower WITHOUT +12 bonus (Odd)!
        var sabOdd = combatState.CreateCard<SwordAndBoard>(player);
        await CardPileCmd.Add(sabOdd, PileType.Hand);
        AutoTestAssert.Equal(CardType.Skill, sabOdd.Type, "SwordAndBoard is Skill when next play is Odd");
        AutoTestAssert.Equal(TargetType.Self, sabOdd.TargetType, "SwordAndBoard targets Self when Odd");
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(sabOdd.TryManualPlay(null), "Play SwordAndBoard (#3 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 8, player.Creature.Block, "SwordAndBoard Odd gave 8 Block (no GammaWard bonus on Odd)");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<GammaWardPower>(), "GammaWardPower consumed by next Skill");

        // Card #4 (Even): GammaWard -> GammaWardPower(12).
        var gamma2 = combatState.CreateCard<GammaWard>(player);
        await CardPileCmd.Add(gamma2, PileType.Hand);
        AutoTestAssert.True(gamma2.TryManualPlay(null), "Play GammaWard (#4 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<GammaWardPower>(), "GammaWard #4 applied GammaWardPower(12)");

        // Card #5 (Odd): PrimeDirective (Power, NOT Skill -> does NOT consume GammaWardPower!).
        var prime = combatState.CreateCard<PrimeDirective>(player);
        await CardPileCmd.Add(prime, PileType.Hand);
        AutoTestAssert.True(prime.TryManualPlay(null), "Play PrimeDirective (#5 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<PrimeDirectivePower>(), "PrimeDirectivePower(4) applied");
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<GammaWardPower>(), "Power card did not consume GammaWardPower");

        // Card #6 (Even): QuickReflex (NOT First -> 6 Block) + GammaWardPower triggers on Even Skill (+12 Block) = +18 Block!
        var quickNonFirst = combatState.CreateCard<QuickReflex>(player);
        await CardPileCmd.Add(quickNonFirst, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(quickNonFirst.TryManualPlay(null), "Play QuickReflex (#6 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 18, player.Creature.Block, "QuickReflex non-First (6) + GammaWard Even Skill (+12) = 18 Block");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<GammaWardPower>(), "GammaWardPower removed after triggering");

        // Card #7 (Odd): WitzBolt -> 8 + 2 (Str) + 4 (PrimeDirective Odd!) = 14 dmg + 1 Weak + 1 Vulnerable!
        // Also verify PrimeDirectivePower hover preview (cardPlay == null): Odd Attack gets +4, Odd Skill SwordAndBoard gets 0!
        var witz = combatState.CreateCard<WitzBolt>(player);
        await CardPileCmd.Add(witz, PileType.Hand);
        var primePower = AutoTestAssert.NotNull(player.Creature.GetPower<PrimeDirectivePower>(), "PrimeDirectivePower instance");
        AutoTestAssert.Equal(4m, primePower.ModifyDamageAdditive(enemy, 8m, ValueProp.Move, player.Creature, witz, null), "PrimeDirective previews +4 on Odd Attack");
        AutoTestAssert.Equal(0m, primePower.ModifyDamageAdditive(enemy, 10m, ValueProp.Move, player.Creature, sabOdd, null), "PrimeDirective previews 0 on Odd Skill SwordAndBoard");
        int hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(witz.TryManualPlay(enemy), "Play WitzBolt (#7 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 14, enemy.CurrentHp, "WitzBolt Odd dealt 8+2+4=14 damage");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "WitzBolt Odd applied 1 Weak");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "WitzBolt Odd applied 1 Vulnerable");

        // Card #8 (Even): WindUpBomb acts as Skill -> buffs its own damage from 8 to 23!
        // Also verify PrimeDirectivePower hover preview on Even Attack is 0.
        AutoTestAssert.Equal(0m, primePower.ModifyDamageAdditive(enemy, 8m, ValueProp.Move, player.Creature, witz, null), "PrimeDirective previews 0 on Even Attack");
        var bomb = combatState.CreateCard<WindUpBomb>(player);
        await CardPileCmd.Add(bomb, PileType.Hand);
        AutoTestAssert.Equal(CardType.Skill, bomb.Type, "WindUpBomb is Skill when next play is Even");
        AutoTestAssert.True(bomb.TryManualPlay(null), "Play WindUpBomb (#8 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(23, bomb.DynamicVars.Damage.IntValue, "WindUpBomb Even increased damage 8 -> 23");

        // Card #9 (Odd): WindUpBomb acts as Attack -> deals floor((23 + 2 Str + 4 PrimeDirective) * 1.5 Vuln) = 43 dmg!
        await CardPileCmd.Add(bomb, PileType.Hand);
        AutoTestAssert.Equal(CardType.Attack, bomb.Type, "WindUpBomb is Attack when next play is Odd");
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(bomb.TryManualPlay(enemy), "Play WindUpBomb (#9 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 43, enemy.CurrentHp, "WindUpBomb Odd dealt floor(29*1.5)=43 damage");

        // Card #10 (Even): SwordAndBoard acts as Attack -> deals floor((10 + 2 Str + 0 PrimeDirective Even!) * 1.5 Vuln) = 18 dmg!
        await CardPileCmd.Add(sabOdd, PileType.Hand);
        AutoTestAssert.Equal(CardType.Attack, sabOdd.Type, "SwordAndBoard is Attack when next play is Even");
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(sabOdd.TryManualPlay(enemy), "Play SwordAndBoard (#10 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 18, enemy.CurrentHp, "SwordAndBoard Even dealt floor(12*1.5)=18 damage (no PrimeDirective bonus)");

        // === TURN 2: QuickReflex First, Flourish Even (Heal) & Odd (Energy) ===
        await EndTurnAndWaitForRoundAsync(player, 2);
        await RefillEnergy(15);
        // Ensure player is missing some HP so Flourish Even healing is observable.
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 20m, ValueProp.Unblockable | ValueProp.Unpowered, null, null);

        // Card #1 (First): QuickReflex -> 6 + 6 = 12 Block!
        var quickFirst = combatState.CreateCard<QuickReflex>(player);
        await CardPileCmd.Add(quickFirst, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(quickFirst.TryManualPlay(null), "Play QuickReflex (#1 First)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 12, player.Creature.Block, "QuickReflex First granted 6+6=12 Block");

        // Card #2 (Even): Flourish -> +3 Plating + Heal 3 HP!
        var flourishEven = combatState.CreateCard<Flourish>(player);
        await CardPileCmd.Add(flourishEven, PileType.Hand);
        int playerHpBefore = player.Creature.CurrentHp;
        AutoTestAssert.True(flourishEven.TryManualPlay(null), "Play Flourish (#2 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, player.Creature.GetPowerAmount<PlatingPower>(), "Flourish applied 3 Plating");
        AutoTestAssert.Equal(playerHpBefore + 3, player.Creature.CurrentHp, "Flourish Even healed 3 HP");

        // Card #3 (Odd): Flourish -> +3 Plating (total 6) + Gain 1 Energy (net 0 energy spent)!
        var flourishOdd = combatState.CreateCard<Flourish>(player);
        await CardPileCmd.Add(flourishOdd, PileType.Hand);
        int energyBefore = pcs.Energy;
        AutoTestAssert.True(flourishOdd.TryManualPlay(null), "Play Flourish (#3 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<PlatingPower>(), "Flourish Odd stacked Plating to 6");
        AutoTestAssert.Equal(energyBefore, pcs.Energy, "Flourish Odd spent 1 and gained 1 Energy");

        // Negative parity branches & GammaWard chaining on Turn 2:
        // Card #4 (Even): GammaWard #1 -> GammaWardPower(12).
        var chainGamma1 = combatState.CreateCard<GammaWard>(player);
        await CardPileCmd.Add(chainGamma1, PileType.Hand);
        AutoTestAssert.True(chainGamma1.TryManualPlay(null), "Play chain GammaWard #1 (#4 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<GammaWardPower>(), "Chain GammaWard #1 applied GammaWardPower(12)");

        // Card #5 (Odd): non-First ElementOfSurprise -> no DuplicationPower; consumes GammaWardPower WITHOUT bonus Block.
        var nonFirstSurprise = combatState.CreateCard<ElementOfSurprise>(player);
        await CardPileCmd.Add(nonFirstSurprise, PileType.Hand);
        AutoTestAssert.True(nonFirstSurprise.TryManualPlay(null), "Play non-First ElementOfSurprise (#5 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<DuplicationPower>(), "Non-First ElementOfSurprise did not apply DuplicationPower");

        // Card #6 (Even): GammaWard #2 -> applies GammaWardPower(12); Card #7 (Odd): Attack; Card #8 (Even): GammaWard #3 -> +16 Block AND keeps GammaWardPower(12)!
        var chainGamma2 = combatState.CreateCard<GammaWard>(player);
        var chainStrike = combatState.CreateCard<PackmasterStrike>(player);
        var chainGamma3 = combatState.CreateCard<GammaWard>(player);
        await CardPileCmd.Add(chainGamma2, PileType.Hand);
        await CardPileCmd.Add(chainStrike, PileType.Hand);
        await CardPileCmd.Add(chainGamma3, PileType.Hand);
        AutoTestAssert.True(chainGamma2.TryManualPlay(null), "Play chain GammaWard #2 (#6 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(chainStrike.TryManualPlay(enemy), "Play spacer Strike (#7 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(chainGamma3.TryManualPlay(null), "Play chain GammaWard #3 (#8 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 16, player.Creature.Block, "Chained Even GammaWard gained exact 12 (prior power) + 4 (self) = 16 Block");
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<GammaWardPower>(), "Chained Even GammaWard preserved newly applied GammaWardPower(12)");

        // Card #9 (Odd): non-First Battlecry (no extra Strength); Card #10 (Even): WitzBolt (no extra Weak/Vuln).
        var nonFirstBattlecry = combatState.CreateCard<Battlecry>(player);
        var evenWitz = combatState.CreateCard<WitzBolt>(player);
        await CardPileCmd.Add(nonFirstBattlecry, PileType.Hand);
        await CardPileCmd.Add(evenWitz, PileType.Hand);
        int strBefore = player.Creature.GetPowerAmount<StrengthPower>();
        AutoTestAssert.True(nonFirstBattlecry.TryManualPlay(null), "Play non-First Battlecry (#9 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(strBefore, player.Creature.GetPowerAmount<StrengthPower>(), "Non-First Battlecry did not grant Strength");
        int weakBefore = enemy.GetPowerAmount<WeakPower>();
        int vulnBefore = enemy.GetPowerAmount<VulnerablePower>();
        AutoTestAssert.True(evenWitz.TryManualPlay(enemy), "Play Even WitzBolt (#10 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(weakBefore, enemy.GetPowerAmount<WeakPower>(), "Even WitzBolt did not apply Weak");
        AutoTestAssert.Equal(vulnBefore, enemy.GetPowerAmount<VulnerablePower>(), "Even WitzBolt did not apply Vulnerable");

        // Card #11 (Odd): Odd GammaWard -> does NOT apply GammaWardPower!
        var oddGamma = combatState.CreateCard<GammaWard>(player);
        await CardPileCmd.Add(oddGamma, PileType.Hand);
        AutoTestAssert.True(oddGamma.TryManualPlay(null), "Play Odd GammaWard (#11 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<GammaWardPower>(), "Odd GammaWard did not apply GammaWardPower");

        // Re-apply GammaWardPower(12) on Card #12 (Even) and leave unused -> verify it expires on Turn 3!
        var expiringGamma = combatState.CreateCard<GammaWard>(player);
        await CardPileCmd.Add(expiringGamma, PileType.Hand);
        AutoTestAssert.True(expiringGamma.TryManualPlay(null), "Play expiring GammaWard (#12 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(12, player.Creature.GetPowerAmount<GammaWardPower>(), "Expiring GammaWard applied GammaWardPower(12)");

        // === TURN 3: GammaWardPower expiration, ElementOfSurprise (First -> DuplicationPower), & SwordAndBoard Type lock w/ BurstPower ===
        await EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<GammaWardPower>(), "Unused GammaWardPower expired at end of turn");
        await RefillEnergy(10);
        var surprise = combatState.CreateCard<ElementOfSurprise>(player);
        var dupStrike = combatState.CreateCard<PackmasterStrike>(player);
        await CardPileCmd.Add(surprise, PileType.Hand);
        await CardPileCmd.Add(dupStrike, PileType.Hand);
        AutoTestAssert.True(surprise.TryManualPlay(null), "Play ElementOfSurprise (#1 First)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DuplicationPower>(), "ElementOfSurprise First applied DuplicationPower(1)");

        int effectiveHpBefore = enemy.CurrentHp + enemy.Block;
        AutoTestAssert.True(dupStrike.TryManualPlay(enemy), "Play duplicated Strike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        // Play #2 (Even): 6+2=8 dmg; duplicate play #3 (Odd!): 6+2+4=12 dmg -> exact 20 total damage!
        AutoTestAssert.Equal(effectiveHpBefore - 20, enemy.CurrentHp + enemy.Block, "Duplicated Strike dealt exact 8 (Even) + 12 (Odd w/ PrimeDirective) = 20 damage");

        // Verify SwordAndBoard Type lock during play with BurstPower: Card #4 (Even, Attack) -> Card #5 (Odd, Skill w/ BurstPower -> plays twice & decrements BurstPower to 0!).
        var sabBurst = combatState.CreateCard<SwordAndBoard>(player);
        var spacer = combatState.CreateCard<PackmasterStrike>(player);
        await CardPileCmd.Add(spacer, PileType.Hand);
        await CardPileCmd.Add(sabBurst, PileType.Hand);
        AutoTestAssert.True(spacer.TryManualPlay(enemy), "Play spacer Strike (#4 Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        await PowerCmd.Apply<BurstPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1m, player.Creature, null);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(sabBurst.TryManualPlay(null), "Play Odd Skill SwordAndBoard with BurstPower (#5 Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<BurstPower>(), "BurstPower decremented on Odd Skill SwordAndBoard (Type did not flip mid-play)");
        AutoTestAssert.True(player.Creature.Block >= blockBefore + 8, "Odd Skill SwordAndBoard gained Block");

        // === TURN 4: LeftHook (Odd stays in hand, Even discards, Duplication at UsesLeft==2, 7th play exhausts!) ===
        await EndTurnAndWaitForRoundAsync(player, 4);
        await RefillEnergy(10);
        var leftHook = combatState.CreateCard<LeftHook>(player);
        await CardPileCmd.Add(leftHook, PileType.Hand);

        // Play #1 (Odd): stays in hand, UsesLeft 7 -> 6.
        AutoTestAssert.True(leftHook.TryManualPlay(enemy), "Play LeftHook #1 (Odd)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.Hand.Cards.Contains(leftHook), "LeftHook Odd stayed in hand");
        AutoTestAssert.Equal(6, leftHook.DynamicVars["UsesLeft"].IntValue, "LeftHook UsesLeft 7 -> 6");

        // Play #2 (Even): goes to Discard, UsesLeft 6 -> 5.
        AutoTestAssert.True(leftHook.TryManualPlay(enemy), "Play LeftHook #2 (Even)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.DiscardPile.Cards.Contains(leftHook), "LeftHook Even went to Discard");
        AutoTestAssert.Equal(5, leftHook.DynamicVars["UsesLeft"].IntValue, "LeftHook UsesLeft 6 -> 5");

        // Play 3 more times (UsesLeft 5 -> 2).
        for (int play = 3; play <= 5; play++)
        {
            if (leftHook.Pile?.Type != PileType.Hand)
                await CardPileCmd.Add(leftHook, PileType.Hand);
            AutoTestAssert.True(leftHook.TryManualPlay(enemy), $"Play LeftHook #{play}");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        AutoTestAssert.Equal(2, leftHook.DynamicVars["UsesLeft"].IntValue, "LeftHook at UsesLeft == 2");

        // Play with DuplicationPower at UsesLeft == 2 -> decrements once (2 -> 1), stays playable, never stuck at 0!
        if (leftHook.Pile?.Type != PileType.Hand)
            await CardPileCmd.Add(leftHook, PileType.Hand);
        await PowerCmd.Apply<DuplicationPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1m, player.Creature, null);
        AutoTestAssert.True(leftHook.TryManualPlay(enemy), "Play duplicated LeftHook at UsesLeft == 2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, leftHook.DynamicVars["UsesLeft"].IntValue, "Duplicated LeftHook decremented UsesLeft once (2 -> 1)");

        // Final play (UsesLeft 1 -> 0) -> Exhausts!
        if (leftHook.Pile?.Type != PileType.Hand)
            await CardPileCmd.Add(leftHook, PileType.Hand);
        AutoTestAssert.True(leftHook.TryManualPlay(enemy), "Play final LeftHook (7th)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(leftHook), "LeftHook exhausted on 7th play");
    }

    private static async Task TestHighEnergyPackInCombat()
    {
        var (player, _, combatState) = await StartCombatAsync("PMENER01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");

        await CreatureCmd.GainMaxHp(enemy, 800m);

        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);

        async Task SetEnergyAsync(int target)
        {
            if (pcs.Energy < target)
                await PlayerCmd.GainEnergy(target - pcs.Energy, player);
            else if (pcs.Energy > target)
                await PlayerCmd.LoseEnergy(pcs.Energy - target, player);
        }

        // 1. Investor (6 Block whenever you spend >= 2 Energy) + StormForm (+2 Energy at turn start).
        // Set Investor's cost to 2 this turn to prove BeforeCardPlayed does NOT self-trigger!
        await SetEnergyAsync(10);
        var investor = combatState.CreateCard<Investor>(player);
        investor.EnergyCost.SetThisTurn(2);
        var stormForm = combatState.CreateCard<StormForm>(player);
        await CardPileCmd.Add(investor, PileType.Hand);
        await CardPileCmd.Add(stormForm, PileType.Hand);
        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(investor.TryManualPlay(null), "Play 2-cost Investor");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<InvestorPower>(), "InvestorPower(6) applied");
        AutoTestAssert.Equal(blockBefore, player.Creature.Block, "2-cost Investor did not self-trigger InvestorPower");

        // Discounted 2-cost card played for 1 Energy must NOT trigger Investor.
        var discountedStrike = combatState.CreateCard<StruckByATrain>(player);
        discountedStrike.EnergyCost.SetThisTurn(1);
        await CardPileCmd.Add(discountedStrike, PileType.Hand);
        AutoTestAssert.True(discountedStrike.TryManualPlay(null), "Play 3-cost card discounted to 1 Energy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore, player.Creature.Block, "Card discounted to 1 Energy did not trigger Investor");

        // Duplicated 3-Energy StormForm should trigger InvestorPower ONLY ONCE (PlayIndex == 0).
        await PowerCmd.Apply<DuplicationPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1m, player.Creature, null);
        AutoTestAssert.True(stormForm.TryManualPlay(null), "Play duplicated StormForm (3 Energy)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, player.Creature.GetPowerAmount<StormFormPower>(), "Duplicated StormForm applied StormFormPower(4)");
        AutoTestAssert.Equal(blockBefore + 6, player.Creature.Block, "Duplicated 3-Energy StormForm triggered Investor exactly once (+6 Block)");

        // 2. Trash: exhausts leftmost card in hand and gains 1 Energy; empty-hand Trash still grants +1 Energy!
        var leftmostFodder = combatState.CreateCard<PackmasterStrike>(player);
        var secondCard = combatState.CreateCard<PackmasterDefend>(player);
        var trash = combatState.CreateCard<Trash>(player);
        await CardPileCmd.Add(leftmostFodder, PileType.Hand);
        await CardPileCmd.Add(secondCard, PileType.Hand);
        await CardPileCmd.Add(trash, PileType.Hand);
        int energyBefore = pcs.Energy;
        AutoTestAssert.True(trash.TryManualPlay(null), "Play Trash");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(leftmostFodder), "Trash exhausted leftmost card");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(secondCard), "Trash left non-leftmost card untouched");
        AutoTestAssert.Equal(energyBefore + 1, pcs.Energy, "Trash gained 1 Energy");

        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        var emptyHandTrash = combatState.CreateCard<Trash>(player);
        await CardPileCmd.Add(emptyHandTrash, PileType.Hand);
        energyBefore = pcs.Energy;
        AutoTestAssert.True(emptyHandTrash.TryManualPlay(null), "Play Trash with empty hand");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBefore + 1, pcs.Energy, "Empty-hand Trash still gained 1 Energy");

        // 3. MiracleWorker: +9 Block, puts Miracle on top of Draw Pile; draw & play Miracle (+1 Energy, Exhaust).
        var miracleWorker = combatState.CreateCard<MiracleWorker>(player);
        await CardPileCmd.Add(miracleWorker, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(miracleWorker.TryManualPlay(null), "Play MiracleWorker");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 9, player.Creature.Block, "MiracleWorker +9 Block");
        var miracle = AutoTestAssert.NotNull(pcs.DrawPile.Cards.FirstOrDefault() as Miracle, "Miracle is on top of Draw Pile");
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1m, player);
        energyBefore = pcs.Energy;
        AutoTestAssert.True(miracle.TryManualPlay(null), "Play Miracle");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(energyBefore + 1, pcs.Energy, "Miracle gained 1 Energy");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(miracle), "Miracle exhausted");

        // 4. LotsOfIronWaves (4 Energy): +22 Block + 22 dmg + Investor (+6 Block).
        await SetEnergyAsync(4);
        var ironWaves = combatState.CreateCard<LotsOfIronWaves>(player);
        await CardPileCmd.Add(ironWaves, PileType.Hand);
        blockBefore = player.Creature.Block;
        int hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(ironWaves.TryManualPlay(enemy), "Play LotsOfIronWaves");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 28, player.Creature.Block, "LotsOfIronWaves (22) + Investor (6) = 28 Block");
        AutoTestAssert.Equal(hpBefore - 22, enemy.CurrentHp, "LotsOfIronWaves 22 damage");

        // 5. LastResort at X = 0 -> applies EnergyNextTurnPower(1) (and does NOT trigger Investor!).
        AutoTestAssert.Equal(0, pcs.Energy, "Energy is 0 before LastResort(0)");
        var lastResortZero = combatState.CreateCard<LastResort>(player);
        await CardPileCmd.Add(lastResortZero, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(lastResortZero.TryManualPlay(null), "Play LastResort at X=0");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore, player.Creature.Block, "LastResort(0) gave 0 Block and did not trigger Investor");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnergyNextTurnPower>(), "LastResort(0) applied EnergyNextTurnPower(1)");

        // 5b. LastResort at X = 3 -> 6x3 = 18 Block + Investor (+6 Block) = 24 Block.
        await SetEnergyAsync(3);
        var lastResortThree = combatState.CreateCard<LastResort>(player);
        await CardPileCmd.Add(lastResortThree, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(lastResortThree.TryManualPlay(null), "Play LastResort at X=3");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore + 24, player.Creature.Block, "LastResort(3) gained 18 + 6 (Investor) = 24 Block");

        // 6. ManifestMeal+ at X = 3 -> 7x3 = 21 dmg, shuffles Food+ (Retain, 3 Energy) into Draw Pile!
        await SetEnergyAsync(3);
        var meal = combatState.CreateCard<ManifestMeal>(player);
        CardCmd.Upgrade(meal);
        await CardPileCmd.Add(meal, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(meal.TryManualPlay(enemy), "Play ManifestMeal+ at X=3");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 21, enemy.CurrentHp, "ManifestMeal+(3) dealt 7x3=21 damage");
        var food = AutoTestAssert.NotNull(pcs.DrawPile.Cards.OfType<Food>().FirstOrDefault(), "ManifestMeal+ shuffled Food+ into Draw Pile");
        AutoTestAssert.True(food.Keywords.Contains(CardKeyword.Retain), "Food+ has Retain");
        AutoTestAssert.Equal(3, food.DynamicVars.Energy.IntValue, "Food+ stores X=3 Energy");
        await CardPileCmd.Add(food, PileType.Hand);
        AutoTestAssert.True(food.TryManualPlay(null), "Play Food+(3)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, pcs.Energy, "Food+(3) granted 3 Energy");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(food), "Food+ exhausted");

        // 6b. ManifestMeal at X = 2 with DuplicationPower(1) -> 4 hits of 6 = 24 dmg + two 2-Energy Foods; plus 0-Energy Food exhausts cleanly!
        await SetEnergyAsync(2);
        await PowerCmd.Apply<DuplicationPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1m, player.Creature, null);
        int foodsInDrawBefore = pcs.DrawPile.Cards.Count(c => c is Food);
        var mealDup = combatState.CreateCard<ManifestMeal>(player);
        await CardPileCmd.Add(mealDup, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(mealDup.TryManualPlay(enemy), "Play duplicated ManifestMeal at X=2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 24, enemy.CurrentHp, "Duplicated ManifestMeal(2) dealt 4x6=24 damage");
        var newFoods = pcs.DrawPile.Cards.OfType<Food>().ToList();
        AutoTestAssert.Equal(foodsInDrawBefore + 2, newFoods.Count, "Duplicated ManifestMeal shuffled 2 Foods into Draw Pile");
        AutoTestAssert.True(newFoods.All(f => f.DynamicVars.Energy.IntValue == 2), "Both duplicated Foods store X=2 Energy");

        var zeroFood = combatState.CreateCard<Food>(player);
        await CardPileCmd.Add(zeroFood, PileType.Hand);
        AutoTestAssert.True(zeroFood.TryManualPlay(null), "Play 0-Energy Food");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, pcs.Energy, "0-Energy Food granted 0 Energy");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(zeroFood), "0-Energy Food exhausted");

        // 7. StruckByATrain: 24 dmg to frontmost enemy, scales to 48 damage!
        await SetEnergyAsync(3);
        var train = combatState.CreateCard<StruckByATrain>(player);
        await CardPileCmd.Add(train, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(train.TryManualPlay(null), "Play StruckByATrain");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 24, enemy.CurrentHp, "StruckByATrain dealt 24 damage");
        AutoTestAssert.Equal(48, train.DynamicVars.Damage.IntValue, "StruckByATrain scaled 24 -> 48 damage");

        // 8. SuperNova (6 Energy): ALL enemies lose 60 HP bypassing Block + 3 Weak + 3 Vulnerable.
        await SetEnergyAsync(6);
        await CreatureCmd.GainBlock(enemy, 20m, ValueProp.Unpowered, null);
        var superNova = combatState.CreateCard<SuperNova>(player);
        await CardPileCmd.Add(superNova, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(superNova.TryManualPlay(null), "Play SuperNova");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 60, enemy.CurrentHp, "SuperNova dealt 60 HP loss bypassing 20 Block");
        AutoTestAssert.Equal(20, enemy.Block, "SuperNova left enemy Block untouched (Unblockable HP loss)");
        AutoTestAssert.Equal(3, enemy.GetPowerAmount<WeakPower>(), "SuperNova applied 3 Weak");
        AutoTestAssert.Equal(3, enemy.GetPowerAmount<VulnerablePower>(), "SuperNova applied 3 Vulnerable");

        // 9. MultiTool: 7 dmg + draw 1 + MultiToolPower(7); Trash+ Retains across turns; end Turn 1 with 1 unspent Energy!
        await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), enemy, enemy.Block, null);
        await SetEnergyAsync(2);
        var trashPlus = combatState.CreateCard<Trash>(player);
        CardCmd.Upgrade(trashPlus);
        await CardPileCmd.Add(trashPlus, PileType.Hand);
        var multiTool = combatState.CreateCard<MultiTool>(player);
        await CardPileCmd.Add(multiTool, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        int handBeforeTool = pcs.Hand.Cards.Count;
        AutoTestAssert.True(multiTool.TryManualPlay(enemy), "Play MultiTool");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        // Enemy has Vulnerable (from SuperNova): floor(7 * 1.5) = 10 dmg!
        AutoTestAssert.Equal(hpBefore - 10, enemy.CurrentHp, "MultiTool dealt 7*1.5=10 Vulnerable damage");
        AutoTestAssert.Equal(handBeforeTool, pcs.Hand.Cards.Count, "MultiTool drew 1 replacement card");
        AutoTestAssert.Equal(7, player.Creature.GetPowerAmount<MultiToolPower>(), "MultiToolPower(7) applied");
        AutoTestAssert.Equal(1, pcs.Energy, "1 unspent Energy remaining before end of Turn 1");

        int enemyHpBeforeTurnEnd = enemy.CurrentHp;
        await EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.Equal(enemyHpBeforeTurnEnd - 7, enemy.CurrentHp, "MultiTool dealt 7 HP loss on turn end with unused Energy");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<MultiToolPower>(), "MultiToolPower expired after turn end");
        AutoTestAssert.True(pcs.Hand.Cards.Contains(trashPlus), "Trash+ Retained across Turn 1 -> Turn 2");
        AutoTestAssert.Equal(8, pcs.Energy, "Turn 2 Energy = 3 base + 4 StormForm (duplicated) + 1 EnergyNextTurn");

        // 9b. MultiTool at 0 unspent Energy -> deals 0 HP loss and still removes MultiToolPower!
        await SetEnergyAsync(1);
        var multiToolZero = combatState.CreateCard<MultiTool>(player);
        await CardPileCmd.Add(multiToolZero, PileType.Hand);
        AutoTestAssert.True(multiToolZero.TryManualPlay(enemy), "Play MultiTool spending last Energy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(0, pcs.Energy, "0 unspent Energy remaining");
        AutoTestAssert.Equal(7, player.Creature.GetPowerAmount<MultiToolPower>(), "MultiToolPower(7) applied on Turn 2");

        int enemyHpBeforeZeroEnd = enemy.CurrentHp;
        await EndTurnAndWaitForRoundAsync(player, 3);
        AutoTestAssert.Equal(enemyHpBeforeZeroEnd, enemy.CurrentHp, "MultiTool dealt 0 HP loss when ending turn with 0 Energy");
        AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<MultiToolPower>(), "MultiToolPower still expired at 0 Energy");
    }

    private static async Task TestBatterPackInCombat()
    {
        var (player, _, combatState) = await StartCombatAsync("PMBATT01");
        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");

        await CreatureCmd.GainMaxHp(enemy, 800m);

        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);

        async Task RefillEnergy(int target = 15)
        {
            if (pcs.Energy < target)
                await PlayerCmd.GainEnergy(target - pcs.Energy, player);
        }

        // 1. WideAngle: steals up to 7 Block from enemy (10 -> 3) and applies 1 Weak.
        await RefillEnergy();
        await CreatureCmd.GainBlock(enemy, 10m, ValueProp.Unpowered, null);
        var wideAngle = combatState.CreateCard<WideAngle>(player);
        await CardPileCmd.Add(wideAngle, PileType.Hand);
        int blockBefore = player.Creature.Block;
        AutoTestAssert.True(wideAngle.TryManualPlay(enemy), "Play WideAngle");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(3, enemy.Block, "WideAngle stole 7 of enemy's 10 Block");
        AutoTestAssert.Equal(blockBefore + 7, player.Creature.Block, "WideAngle granted 7 stolen Block");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<WeakPower>(), "WideAngle applied 1 Weak");

        // Clear enemy block and also verify WideAngle against 0-Block enemy grants 0 Block + still applies Weak!
        await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), enemy, enemy.Block, null);
        var wideAngleZero = combatState.CreateCard<WideAngle>(player);
        await CardPileCmd.Add(wideAngleZero, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(wideAngleZero.TryManualPlay(enemy), "Play WideAngle against 0-Block enemy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore, player.Creature.Block, "WideAngle against 0-Block enemy gained 0 Block");
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<WeakPower>(), "WideAngle against 0-Block enemy still applied 1 Weak");

        // 2. Homerun on Turn 1 already has 2 prior cards (wideAngle, wideAngleZero) -> 1.5x crit (24 dmg)!
        // Test non-crit Homerun on Turn 2 (0 prior cards) below.
        var homerunCrit = combatState.CreateCard<Homerun>(player);
        await CardPileCmd.Add(homerunCrit, PileType.Hand);
        int hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(homerunCrit.TryManualPlay(enemy), "Play Homerun with 2 prior cards");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 24, enemy.CurrentHp, "Homerun dealt 1.5x crit (24) damage with >= 2 prior cards");

        // 4. UltimateHomerun: highest unblocked attack damage so far is 24 -> verify InCombat description AND 24 damage!
        var ultHomerun = combatState.CreateCard<UltimateHomerun>(player);
        await CardPileCmd.Add(ultHomerun, PileType.Hand);
        string ultDesc = ultHomerun.GetDescriptionForPile(PileType.Hand);
        AutoTestAssert.True(!ultDesc.Contains('{') && !ultDesc.Contains('}') && ultDesc.Contains("24"), $"UltimateHomerun InCombat description rendered cleanly: {ultDesc}");
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(ultHomerun.TryManualPlay(enemy), "Play UltimateHomerun");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 24, enemy.CurrentHp, "UltimateHomerun dealt highest prior unblocked attack damage (24)");

        // 5. OptimizedBlur (+2 Block per unblocked attack), HardCaramel (apply 1 Vuln if none), AvatarBeat (+2 Str + draw 1 at >=10, raises to 15).
        await RefillEnergy();
        var blur = combatState.CreateCard<OptimizedBlur>(player);
        var caramel = combatState.CreateCard<HardCaramel>(player);
        var avatar = combatState.CreateCard<AvatarBeat>(player);
        await CardPileCmd.Add(blur, PileType.Hand);
        await CardPileCmd.Add(caramel, PileType.Hand);
        await CardPileCmd.Add(avatar, PileType.Hand);
        AutoTestAssert.True(blur.TryManualPlay(null), "Play OptimizedBlur");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(caramel.TryManualPlay(null), "Play HardCaramel");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.True(avatar.TryManualPlay(null), "Play AvatarBeat");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<OptimizedBlurPower>(), "OptimizedBlurPower(2)");
        AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<HardCaramelPower>(), "HardCaramelPower(1)");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<AvatarBeatPower>(), "AvatarBeatPower(2)");

        // 5b. Blocked attack gate: enemy has 20 Block -> 6-dmg Strike deals 0 unblocked dmg -> OptimizedBlur does NOT grant Block, HardCaramel DOES apply 1 Vuln!
        await CreatureCmd.GainBlock(enemy, 20m, ValueProp.Unpowered, null);
        var blockedStrike = combatState.CreateCard<PackmasterStrike>(player);
        await CardPileCmd.Add(blockedStrike, PileType.Hand);
        blockBefore = player.Creature.Block;
        AutoTestAssert.True(blockedStrike.TryManualPlay(enemy), "Play Strike fully blocked by enemy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(blockBefore, player.Creature.Block, "OptimizedBlur did not trigger on 0 unblocked damage");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "HardCaramel still applied 1 Vulnerable on blocked attack");
        await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), enemy, enemy.Block, null);
        await PowerCmd.Remove<VulnerablePower>(enemy);

        // 6. BatterUp against attacking enemy (Nibbit Turn 1 BUTT_MOVE): exact 1.5x crit (15 dmg) -> triggers OptimizedBlur (+2 Block), HardCaramel (1 Vuln), AvatarBeat (+2 Str, +1 draw, threshold 10 -> 15)!
        AutoTestAssert.True(enemy.Monster?.IntendsToAttack == true, "Seed PMBATT01 Turn-1 enemy intends to attack so BatterUp crit is exercised");
        var batterUp = combatState.CreateCard<BatterUp>(player);
        await CardPileCmd.Add(batterUp, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        blockBefore = player.Creature.Block;
        int handBeforeBatter = pcs.Hand.Cards.Count;
        AutoTestAssert.True(batterUp.TryManualPlay(enemy), "Play BatterUp (crit)");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 15, enemy.CurrentHp, "BatterUp crit dealt 10*1.5=15 damage");
        AutoTestAssert.Equal(blockBefore + 2, player.Creature.Block, "OptimizedBlur granted +2 Block on unblocked attack");
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "HardCaramel applied 1 Vulnerable");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "AvatarBeat granted +2 Strength (>= 10 dmg)");
        AutoTestAssert.Equal(handBeforeBatter, pcs.Hand.Cards.Count, "AvatarBeat drew 1 replacement card");
        var avatarPower = AutoTestAssert.NotNull(player.Creature.GetPower<AvatarBeatPower>(), "AvatarBeatPower");
        AutoTestAssert.Equal(15, avatarPower.DynamicVars["Threshold"].IntValue, "AvatarBeat raised threshold 10 -> 15");

        // Verify HardCaramel does NOT apply extra Vulnerable when target already has Vulnerable!
        var followStrike = combatState.CreateCard<PackmasterStrike>(player);
        await CardPileCmd.Add(followStrike, PileType.Hand);
        AutoTestAssert.True(followStrike.TryManualPlay(enemy), "Play follow-up Strike against Vulnerable enemy");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(1, enemy.GetPowerAmount<VulnerablePower>(), "HardCaramel did not add Vulnerable when target already had it");

        // 7. WindUp: applies 1 Vulnerable (1 -> 2), gains 1 Energy, Exhausts.
        var windUp = combatState.CreateCard<WindUp>(player);
        await CardPileCmd.Add(windUp, PileType.Hand);
        int energyBefore = pcs.Energy;
        AutoTestAssert.True(windUp.TryManualPlay(enemy), "Play WindUp");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(2, enemy.GetPowerAmount<VulnerablePower>(), "WindUp applied 1 Vulnerable");
        AutoTestAssert.Equal(energyBefore + 1, pcs.Energy, "WindUp gained 1 Energy");
        AutoTestAssert.True(pcs.ExhaustPile.Cards.Contains(windUp), "WindUp exhausted");

        // 8. Tragedy: 5 cards in hand -> non-crit (13 dmg < 15 threshold -> AvatarBeat does NOT trigger!); then stack 2nd AvatarBeat (Amount=4, Draws=2, Threshold stays 15) -> Tragedy with 6 cards -> 20 dmg >= 15 -> +4 Str, +2 cards drawn, Threshold 15 -> 20!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        for (int i = 0; i < 4; i++)
            await CardPileCmd.Add(combatState.CreateCard<PackmasterDefend>(player), PileType.Hand);
        var tragedy5 = combatState.CreateCard<Tragedy>(player);
        await CardPileCmd.Add(tragedy5, PileType.Hand);
        AutoTestAssert.Equal(5, pcs.Hand.Cards.Count, "5 cards in hand before non-crit Tragedy");
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(tragedy5.TryManualPlay(null), "Play Tragedy with 5 cards");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 13, enemy.CurrentHp, "Tragedy with 5 cards dealt non-crit floor(9*1.5)=13 damage");
        AutoTestAssert.Equal(2, player.Creature.GetPowerAmount<StrengthPower>(), "13 < 15 threshold did not trigger AvatarBeat");
        AutoTestAssert.Equal(15, avatarPower.DynamicVars["Threshold"].IntValue, "AvatarBeat threshold stayed at 15");

        // Stack a 2nd AvatarBeat: Amount 2 -> 4, Draws 1 -> 2, Threshold stays 15!
        var avatar2 = combatState.CreateCard<AvatarBeat>(player);
        await CardPileCmd.Add(avatar2, PileType.Hand);
        AutoTestAssert.True(avatar2.TryManualPlay(null), "Play 2nd AvatarBeat to stack");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(4, avatarPower.Amount, "2nd AvatarBeat stacked Amount 2 -> 4");
        AutoTestAssert.Equal(2, avatarPower.DynamicVars["Draws"].IntValue, "2nd AvatarBeat stacked Draws 1 -> 2");
        AutoTestAssert.Equal(15, avatarPower.DynamicVars["Threshold"].IntValue, "2nd AvatarBeat kept Threshold at 15");

        // Add 1 Defend + Tragedy = 6 cards total -> 1.5x crit: floor((7 + 2 Str) * 1.5 * 1.5) = 20 dmg >= 15 -> AvatarBeat triggers (+4 Str -> 6, +2 draws, Threshold -> 20)!
        await CardPileCmd.Add(combatState.CreateCard<PackmasterDefend>(player), PileType.Hand);
        var tragedy6 = combatState.CreateCard<Tragedy>(player);
        await CardPileCmd.Add(tragedy6, PileType.Hand);
        AutoTestAssert.Equal(6, pcs.Hand.Cards.Count, "6 cards in hand before crit Tragedy");
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(tragedy6.TryManualPlay(null), "Play Tragedy with 6 cards");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 20, enemy.CurrentHp, "Tragedy with 6 cards dealt 1.5x crit + Vuln = 20 damage");
        AutoTestAssert.Equal(6, player.Creature.GetPowerAmount<StrengthPower>(), "Stacked AvatarBeat granted +4 Strength (2 -> 6)");
        AutoTestAssert.Equal(7, pcs.Hand.Cards.Count, "Stacked AvatarBeat drew 2 cards (5 remaining + 2 drawn = 7)");
        AutoTestAssert.Equal(20, avatarPower.DynamicVars["Threshold"].IntValue, "AvatarBeat raised threshold 15 -> 20");

        // 8b. UltimateHomerun with 6 Strength + Vulnerable: base 24 + 6 Str = 30, * 1.5 Vuln = 45 dmg!
        var ultScaled = combatState.CreateCard<UltimateHomerun>(player);
        await CardPileCmd.Add(ultScaled, PileType.Hand);
        hpBefore = enemy.CurrentHp;
        AutoTestAssert.True(ultScaled.TryManualPlay(enemy), "Play UltimateHomerun with 6 Strength + Vulnerable");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(hpBefore - 45, enemy.CurrentHp, "UltimateHomerun scaled with Strength (24+6=30) and Vulnerable (*1.5=45)");

        // 9. OpenBracket: draws 2 cards, prevents playing Skills for rest of turn, allows Attacks, expires on Turn 2!
        foreach (var c in pcs.Hand.Cards.ToList())
            await CardPileCmd.Add(c, PileType.Discard);
        await RefillEnergy();
        var openBracket = combatState.CreateCard<OpenBracket>(player);
        var blockedDefend = combatState.CreateCard<PackmasterDefend>(player);
        var allowedStrike = combatState.CreateCard<PackmasterStrike>(player);
        await CardPileCmd.Add(openBracket, PileType.Hand);
        await CardPileCmd.Add(blockedDefend, PileType.Hand);
        await CardPileCmd.Add(allowedStrike, PileType.Hand);
        int handBeforeBracket = pcs.Hand.Cards.Count;
        AutoTestAssert.True(openBracket.TryManualPlay(null), "Play OpenBracket");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(handBeforeBracket - 1 + 2, pcs.Hand.Cards.Count, "OpenBracket drew 2 cards");
        AutoTestAssert.True(player.Creature.HasPower<OpenBracketPower>(), "OpenBracketPower applied");
        AutoTestAssert.True(!blockedDefend.CanPlay(), "OpenBracketPower prevents playing Skills");
        AutoTestAssert.True(allowedStrike.CanPlay(), "OpenBracketPower still allows playing Attacks");
        AutoTestAssert.True(allowedStrike.TryManualPlay(enemy), "Play Attack under OpenBracketPower");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

        await EndTurnAndWaitForRoundAsync(player, 2);
        AutoTestAssert.True(!player.Creature.HasPower<OpenBracketPower>(), "OpenBracketPower expired on Turn 2");

        // 10. Non-crit Homerun on Turn 2 (0 prior cards played this turn -> no 1.5x crit)!
        await RefillEnergy();
        await PowerCmd.Remove<VulnerablePower>(enemy);
        var homerunNoCrit = combatState.CreateCard<Homerun>(player);
        await CardPileCmd.Add(homerunNoCrit, PileType.Hand);
        int strOnTurn2 = player.Creature.GetPowerAmount<StrengthPower>();
        int effectiveHpBefore = enemy.CurrentHp + enemy.Block;
        AutoTestAssert.True(homerunNoCrit.TryManualPlay(enemy), "Play non-crit Homerun as 1st card of Turn 2");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(effectiveHpBefore - (16 + strOnTurn2), enemy.CurrentHp + enemy.Block, "Homerun dealt non-crit 16+Str damage with 0 prior cards");
    }

    private static async Task TestPackRunStateSaveLoadPersistence()
    {
        bool savedAllPacks = PackmasterConfig.AllPacksMode;
        PackmasterConfig.AllPacksMode = false;
        try
        {
            var player = Player.CreateForNewRun<ThePackmaster>(new UnlockState(SaveManager.Instance.Progress), 1uL);
            var runState = await NGame.Instance!.StartNewSingleplayerRun(
                player,
                shouldSave: false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                "PMSAVE01",
                GameMode.Standard,
                isCustomGame: false);

            // Lazy fallback query must NOT persist anything yet!
            AutoTestAssert.True(!PackRunState.HasPersistedActivePacks(runState), "Fresh run has no persisted packs before explicit draft");
            var fallback = AutoTestAssert.NotNull(PackRunState.GetActivePackIds(runState), "Lazy fallback pack set");
            AutoTestAssert.True(!PackRunState.HasPersistedActivePacks(runState), "Querying fallback did not mark packs as persisted");
            AutoTestAssert.True(fallback.Contains(PackDefinitions.CoreSetId), "Fallback includes CoreSet");

            // 0. Unpersisted (pre-draft) JSON + Packet round-trip must succeed cleanly without persisting!
            var typeInfo = JsonSerializationUtility.GetTypeInfo<SerializableRun>();
            var preDraftSave = RunManager.Instance.ToSave(null);
            string preDraftJson = JsonSerializer.Serialize(preDraftSave, typeInfo);
            var preDraftJsonLoaded = RunState.FromSerializable(
                AutoTestAssert.NotNull(JsonSerializer.Deserialize(preDraftJson, typeInfo), "Pre-draft JSON save"));
            AutoTestAssert.True(!PackRunState.HasPersistedActivePacks(preDraftJsonLoaded), "Pre-draft JSON round-trip stays unpersisted");

            var preWriter = new PacketWriter();
            preDraftSave.Serialize(preWriter);
            var preReader = new PacketReader();
            preReader.Reset(preWriter.Buffer);
            var prePacketSave = new SerializableRun();
            prePacketSave.Deserialize(preReader);
            var preDraftPacketLoaded = RunState.FromSerializable(prePacketSave);
            AutoTestAssert.True(!PackRunState.HasPersistedActivePacks(preDraftPacketLoaded), "Pre-draft Packet round-trip stays unpersisted");

            // Explicitly persist a distinct subset (different from the 7-pack fallback!).
            string[] customPacks = [PackDefinitions.CoreSetId, PackDefinitions.QuietId, PackDefinitions.BatterId];
            PackRunState.SetActivePacksForRun(runState, customPacks);
            AutoTestAssert.True(PackRunState.HasPersistedActivePacks(runState), "Persisted flag set after SetActivePacksForRun");

            var save = RunManager.Instance.ToSave(null);

            // 1. Real JSON round-trip via StS 2's JsonSerializationUtility!
            string json = JsonSerializer.Serialize(save, typeInfo);
            AutoTestAssert.True(json.Contains("packmaster_active_packs"), "Serialized run JSON contains packmaster_active_packs extended save data");
            var jsonSave = AutoTestAssert.NotNull(
                JsonSerializer.Deserialize(json, typeInfo),
                "Deserialized SerializableRun from JSON");
            var loadedFromJson = RunState.FromSerializable(jsonSave);
            AutoTestAssert.True(PackRunState.HasPersistedActivePacks(loadedFromJson), "Loaded RunState from JSON has persisted packs");
            var jsonPacks = AutoTestAssert.NotNull(PackRunState.GetActivePackIds(loadedFromJson), "Active packs from JSON-loaded RunState");
            AutoTestAssert.Equal(3, jsonPacks.Count, "JSON-loaded pack count matches saved custom subset");
            AutoTestAssert.True(customPacks.All(jsonPacks.Contains), "JSON-loaded packs match exact saved IDs");

            // 1b. Stale/removed pack ID in JSON is cleanly filtered out while valid packs survive!
            string staleJson = json.Replace(PackDefinitions.BatterId, "RemovedPack");
            var staleLoaded = RunState.FromSerializable(
                AutoTestAssert.NotNull(JsonSerializer.Deserialize(staleJson, typeInfo), "Deserialized stale JSON save"));
            var stalePacks = AutoTestAssert.NotNull(PackRunState.GetActivePackIds(staleLoaded), "Active packs after filtering stale ID");
            AutoTestAssert.Equal(2, stalePacks.Count, "Stale pack ID filtered out leaving 2 valid packs");
            AutoTestAssert.True(stalePacks.Contains(PackDefinitions.CoreSetId) && stalePacks.Contains(PackDefinitions.QuietId), "Valid packs survived stale ID filtering");

            // 2. Real multiplayer PacketWriter / PacketReader round-trip!
            var writer = new PacketWriter();
            save.Serialize(writer);
            var reader = new PacketReader();
            reader.Reset(writer.Buffer);
            var packetSave = new SerializableRun();
            packetSave.Deserialize(reader);
            var loadedFromPacket = RunState.FromSerializable(packetSave);
            AutoTestAssert.True(PackRunState.HasPersistedActivePacks(loadedFromPacket), "Loaded RunState from Packet has persisted packs");
            var packetPacks = AutoTestAssert.NotNull(PackRunState.GetActivePackIds(loadedFromPacket), "Active packs from Packet-loaded RunState");
            AutoTestAssert.Equal(3, packetPacks.Count, "Packet-loaded pack count matches saved custom subset");
            AutoTestAssert.True(customPacks.All(packetPacks.Contains), "Packet-loaded packs match exact saved IDs");
        }
        finally
        {
            PackmasterConfig.AllPacksMode = savedAllPacks;
        }
    }

    private static async Task TestNeowInteractivePackDraft()
    {
        bool savedAllPacks = PackmasterConfig.AllPacksMode;
        int savedPacksPerRun = PackmasterConfig.PacksPerRun;
        PackmasterConfig.AllPacksMode = false;
        // With 7 registered packs and PacksPerRun = 5: CoreSet (1) + 1 random + 3 interactive choice picks = 5 packs!
        PackmasterConfig.PacksPerRun = 5;
        try
        {
            var player = Player.CreateForNewRun<ThePackmaster>(new UnlockState(SaveManager.Instance.Progress), 1uL);
            var runState = await NGame.Instance!.StartNewSingleplayerRun(
                player,
                shouldSave: false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                "PMDRAFT1",
                GameMode.Standard,
                isCustomGame: false);

            await RunManager.Instance.EnterRoomDebug(
                RoomType.Event,
                MapPointType.Ancient,
                ModelDb.Event<MegaCrit.Sts2.Core.Models.Events.Neow>(),
                showTransition: false);

            var eventRoom = AutoTestAssert.NotNull(runState.CurrentRoom as EventRoom, "Entered Neow EventRoom");
            var neow = AutoTestAssert.NotNull(eventRoom.LocalMutableEvent as MegaCrit.Sts2.Core.Models.Events.Neow, "Act 1 Floor 1 event is Neow");

            // Draft should NOT be persisted yet; Neow should be presenting Pick 1/3 pack options!
            AutoTestAssert.True(!PackRunState.HasPersistedActivePacks(runState), "Active packs not persisted until final pick");
            AutoTestAssert.Equal(3, neow.CurrentOptions.Count, "Pick 1 presents 3 candidate packs");
            AutoTestAssert.True(neow.CurrentOptions.All(o => o.TextKey.StartsWith("PACKMASTER_DRAFT_1_")), "Pick 1 options are Packmaster draft options");

            // Drive all 3 picks via the REAL UI click path (EventSynchronizer.ChooseLocalOption)!
            var pickedIds = new List<string>();
            for (int pick = 1; pick <= 3; pick++)
            {
                var opt = neow.CurrentOptions[0];
                string prefix = $"PACKMASTER_DRAFT_{pick}_";
                AutoTestAssert.True(opt.TextKey.StartsWith(prefix), $"Pick {pick} option key starts with {prefix}");
                string optTitle = opt.Title.GetFormattedText();
                string optDesc = opt.Description.GetFormattedText();
                AutoTestAssert.True(!string.IsNullOrWhiteSpace(optTitle) && !optTitle.Contains('{'), $"Pick {pick} title rendered cleanly: {optTitle}");
                AutoTestAssert.True(!string.IsNullOrWhiteSpace(optDesc) && !optDesc.Contains('{') && !optDesc.Contains('}'), $"Pick {pick} description rendered cleanly: {optDesc}");

                pickedIds.Add(opt.TextKey[prefix.Length..]);
                RunManager.Instance.EventSynchronizer.ChooseLocalOption(0);
                await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            }

            // Draft complete! Packs persisted, exact count = 5 (CoreSet + 1 random + 3 picked), and Neow now shows normal blessings!
            AutoTestAssert.True(PackRunState.HasPersistedActivePacks(runState), "Active packs persisted after final pick");
            var activePacks = AutoTestAssert.NotNull(PackRunState.GetActivePackIds(runState), "Final drafted pack set");
            AutoTestAssert.Equal(5, activePacks.Count, "Final drafted pack count matches PacksPerRun (5)");
            AutoTestAssert.True(activePacks.Contains(PackDefinitions.CoreSetId), "Final drafted packs include CoreSet");
            AutoTestAssert.True(pickedIds.All(activePacks.Contains), "Final drafted packs include all 3 interactively picked packs");
            AutoTestAssert.True(neow.CurrentOptions.Count > 0, "Neow now presents normal blessing options");
            AutoTestAssert.True(neow.CurrentOptions.All(o => !o.TextKey.StartsWith("PACKMASTER_DRAFT_")), "Neow options are no longer draft options");

            // 4th click: pick a real vanilla Neow blessing via EventSynchronizer to confirm room transitions & finishes cleanly!
            RunManager.Instance.EventSynchronizer.ChooseLocalOption(0);
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            AutoTestAssert.True(neow.IsFinished || neow.CurrentOptions.Any(o => o.IsProceed), "Vanilla Neow blessing completed cleanly after Packmaster draft");

            // Re-entering Neow on the same RunState (already persisted) must skip draft straight to vanilla blessings!
            await RunManager.Instance.EnterRoomDebug(
                RoomType.Event,
                MapPointType.Ancient,
                ModelDb.Event<MegaCrit.Sts2.Core.Models.Events.Neow>(),
                showTransition: false);
            var neowAgain = AutoTestAssert.NotNull(((EventRoom)runState.CurrentRoom!).LocalMutableEvent as MegaCrit.Sts2.Core.Models.Events.Neow, "Re-entered Neow");
            AutoTestAssert.True(neowAgain.CurrentOptions.All(o => !o.TextKey.StartsWith("PACKMASTER_DRAFT_")), "Already-persisted run skips draft straight to vanilla Neow");

            // AllPacksMode == true skips draft completely (does not persist or show draft options).
            CleanupRun();
            PackmasterConfig.AllPacksMode = true;
            var allPacksPlayer = Player.CreateForNewRun<ThePackmaster>(new UnlockState(SaveManager.Instance.Progress), 1uL);
            var allPacksRun = await NGame.Instance!.StartNewSingleplayerRun(
                allPacksPlayer,
                shouldSave: false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                "PMDRAFT2",
                GameMode.Standard,
                isCustomGame: false);
            await RunManager.Instance.EnterRoomDebug(
                RoomType.Event,
                MapPointType.Ancient,
                ModelDb.Event<MegaCrit.Sts2.Core.Models.Events.Neow>(),
                showTransition: false);
            var allPacksNeow = AutoTestAssert.NotNull(((EventRoom)allPacksRun.CurrentRoom!).LocalMutableEvent as MegaCrit.Sts2.Core.Models.Events.Neow, "AllPacksMode Neow");
            AutoTestAssert.True(allPacksNeow.CurrentOptions.All(o => !o.TextKey.StartsWith("PACKMASTER_DRAFT_")), "AllPacksMode skips Neow draft");
            AutoTestAssert.True(!PackRunState.HasPersistedActivePacks(allPacksRun), "AllPacksMode does not persist active packs");

            // PacksPerRun >= AllPacks.Count skips draft and persists all registered packs.
            CleanupRun();
            PackmasterConfig.AllPacksMode = false;
            PackmasterConfig.PacksPerRun = PackRegistry.AllPacks.Count;
            var fullPlayer = Player.CreateForNewRun<ThePackmaster>(new UnlockState(SaveManager.Instance.Progress), 1uL);
            var fullRun = await NGame.Instance!.StartNewSingleplayerRun(
                fullPlayer,
                shouldSave: false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                "PMDRAFT3",
                GameMode.Standard,
                isCustomGame: false);
            await RunManager.Instance.EnterRoomDebug(
                RoomType.Event,
                MapPointType.Ancient,
                ModelDb.Event<MegaCrit.Sts2.Core.Models.Events.Neow>(),
                showTransition: false);
            var fullNeow = AutoTestAssert.NotNull(((EventRoom)fullRun.CurrentRoom!).LocalMutableEvent as MegaCrit.Sts2.Core.Models.Events.Neow, "Full-pack Neow");
            AutoTestAssert.True(fullNeow.CurrentOptions.All(o => !o.TextKey.StartsWith("PACKMASTER_DRAFT_")), "PacksPerRun >= AllPacks.Count skips draft");
            AutoTestAssert.Equal(PackRegistry.AllPacks.Count, PackRunState.GetActivePackIds(fullRun)?.Count, "PacksPerRun >= AllPacks.Count persists all registered packs");
        }
        finally
        {
            PackmasterConfig.AllPacksMode = savedAllPacks;
            PackmasterConfig.PacksPerRun = savedPacksPerRun;
        }
    }

    private sealed class TakeNCardSelector(int count) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            int take = Math.Clamp(count, minSelect, maxSelect);
            return Task.FromResult(options.Take(take));
        }

        public CardRewardSelection GetSelectedCardReward(
            IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) =>
            new AutoSlayCardSelector(new Rng(1)).GetSelectedCardReward(options, alternatives);
    }

    private static async Task TestCorePackmasterRelics()
    {
        bool savedAllPacks = PackmasterConfig.AllPacksMode;
        PackmasterConfig.AllPacksMode = false;
        try
        {
            var player = Player.CreateForNewRun<ThePackmaster>(new UnlockState(SaveManager.Instance.Progress), 1uL);
            var runState = await NGame.Instance!.StartNewSingleplayerRun(
                player,
                shouldSave: false,
                ActModel.GetDefaultList(),
                Array.Empty<ModifierModel>(),
                "PMRELIC1",
                GameMode.Standard,
                isCustomGame: false);

            // Set 3 active packs so GemOfUnity can be cleanly triggered in 3 plays!
            PackRunState.SetActivePacksForRun(runState, [PackDefinitions.CoreSetId, PackDefinitions.QuietId, PackDefinitions.BatterId]);

            // 1. PmBoosterPack: adds 1 card from a registered Packmaster pack to deck!
            var deckSnapshotBeforeBooster = player.Deck.Cards.ToHashSet();
            await RelicCmd.Obtain<PmBoosterPack>(player);
            var addedByBooster = player.Deck.Cards.Where(c => !deckSnapshotBeforeBooster.Contains(c)).ToList();
            AutoTestAssert.Equal(1, addedByBooster.Count, "PmBoosterPack added 1 card to deck");
            AutoTestAssert.NotNull(PackRegistry.GetForCard(addedByBooster[0]), "PmBoosterPack card belongs to a registered Pack");

            // 2. PmCollection: minSelect=0, maxSelect=int.MaxValue; test selector picks 2 cards from the SAME pack (and 0-pick confirm also works)!
            var deckSnapshotBeforeCollection = player.Deck.Cards.ToHashSet();
            var prevSelector = _activeCardSelector;
            _activeCardSelector = new TakeNCardSelector(2);
            try
            {
                await RelicCmd.Obtain<PmCollection>(player);
            }
            finally
            {
                _activeCardSelector = prevSelector;
            }
            var addedByCollection = player.Deck.Cards.Where(c => !deckSnapshotBeforeCollection.Contains(c)).ToList();
            AutoTestAssert.Equal(2, addedByCollection.Count, "PmCollection added 2 chosen pack cards to deck (10+1+2=13)");
            var collectionPack = AutoTestAssert.NotNull(PackRegistry.GetForCard(addedByCollection[0]), "PmCollection card 1 has pack");
            AutoTestAssert.Equal(collectionPack, PackRegistry.GetForCard(addedByCollection[1]), "PmCollection cards come from the same pack");

            // 3. BagOfHolding: allowed when HandyHaversack is owned, removes HandyHaversack, then IsAllowed becomes false!
            AutoTestAssert.True(ModelDb.Relic<BagOfHolding>().IsAllowed(runState), "BagOfHolding allowed while player owns HandyHaversack");
            var bag = await RelicCmd.Obtain<BagOfHolding>(player);
            AutoTestAssert.True(!player.Relics.Any(r => r is HandyHaversack), "BagOfHolding removed HandyHaversack");
            AutoTestAssert.True(!ModelDb.Relic<BagOfHolding>().IsAllowed(runState), "BagOfHolding no longer allowed after HandyHaversack removed");

            // 4. GemOfUnity IsAllowed check: true when !AllPacksMode, false when AllPacksMode == true.
            AutoTestAssert.True(ModelDb.Relic<GemOfUnity>().IsAllowed(runState), "GemOfUnity allowed in normal mode");
            PackmasterConfig.AllPacksMode = true;
            AutoTestAssert.True(!ModelDb.Relic<GemOfUnity>().IsAllowed(runState), "GemOfUnity disallowed in AllPacksMode");
            PackmasterConfig.AllPacksMode = false;

            var badge = await RelicCmd.Obtain<CollectorBadge>(player);
            var gem = await RelicCmd.Obtain<GemOfUnity>(player);

            // Enter combat: Deck has 13 cards -> BagOfHolding gives 13 / 5 = 2 turns of +1 Energy!
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, showTransition: false);
            await WaitUntilAsync(() =>
                CombatManager.Instance.IsInProgress
                && !CombatManager.Instance.PlayerActionsDisabled
                && player.PlayerCombatState is not null
                && !RunManager.Instance.ActionExecutor.IsRunning);

            var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
            var combatState = AutoTestAssert.NotNull(player.Creature.CombatState, "CombatState");
            var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");
            await CreatureCmd.GainMaxHp(enemy, 500m);

            // Turn 1: 3 base + 1 BagOfHolding = 4 Energy (HandyHaversack is gone -> 5-card hand, 4 Energy, BagOfHolding counter = 1 remaining!).
            AutoTestAssert.Equal(5, pcs.Hand.Cards.Count, "Turn-1 hand size is 5 (HandyHaversack was replaced)");
            AutoTestAssert.Equal(4, pcs.Energy, "Turn-1 Energy = 3 base + 1 BagOfHolding");
            AutoTestAssert.Equal(1, bag.DisplayAmount, "BagOfHolding counter decremented 2 -> 1");
            AutoTestAssert.Equal(3, gem.DisplayAmount, "GemOfUnity counter starts at 3 active packs");
            AutoTestAssert.Equal(0, badge.DisplayAmount, "CollectorBadge counter starts at 0");

            // Take 10 HP damage so GemOfUnity's +5 Heal is observable.
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 10m, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
            int hpBeforeGem = player.Creature.CurrentHp;

            foreach (var c in pcs.Hand.Cards.ToList())
                await CardPileCmd.Add(c, PileType.Discard);
            await PlayerCmd.GainEnergy(6, player);

            // Play non-pack Strike (must NOT affect badge or gem!), CoreSet #1, duplicate CoreSet #2, Quiet #1, Batter #1, and inactive Warlock #1 (4th pack -> badge stays capped at 3, gem does NOT re-trigger!).
            var nonPackStrike = combatState.CreateCard<PackmasterStrike>(player);
            var core1 = combatState.CreateCard<Showoff>(player);
            var core2 = combatState.CreateCard<Sideboard>(player);
            var quiet1 = combatState.CreateCard<IronShield>(player);
            var batter1 = combatState.CreateCard<WideAngle>(player);
            var inactiveWarlock = combatState.CreateCard<HighPriestessJeklik>(player);
            await CardPileCmd.Add(nonPackStrike, PileType.Hand);
            await CardPileCmd.Add(core1, PileType.Hand);
            await CardPileCmd.Add(core2, PileType.Hand);
            await CardPileCmd.Add(quiet1, PileType.Hand);
            await CardPileCmd.Add(batter1, PileType.Hand);
            await CardPileCmd.Add(inactiveWarlock, PileType.Hand);

            AutoTestAssert.True(nonPackStrike.TryManualPlay(enemy), "Play non-pack Strike");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            AutoTestAssert.Equal(0, badge.DisplayAmount, "Non-pack card did not increment CollectorBadge");
            AutoTestAssert.Equal(3, gem.DisplayAmount, "Non-pack card did not decrement GemOfUnity");

            AutoTestAssert.True(core1.TryManualPlay(null), "Play CoreSet #1");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            AutoTestAssert.Equal(1, badge.DisplayAmount, "CollectorBadge count = 1 after CoreSet");
            AutoTestAssert.Equal(2, gem.DisplayAmount, "GemOfUnity remaining = 2 after CoreSet");

            AutoTestAssert.True(core2.TryManualPlay(null), "Play CoreSet #2 (duplicate pack)");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            AutoTestAssert.Equal(1, badge.DisplayAmount, "Duplicate pack did not increment CollectorBadge");
            AutoTestAssert.Equal(2, gem.DisplayAmount, "Duplicate pack did not decrement GemOfUnity");

            AutoTestAssert.True(quiet1.TryManualPlay(null), "Play Quiet #1");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            AutoTestAssert.Equal(2, badge.DisplayAmount, "CollectorBadge count = 2 after Quiet");
            AutoTestAssert.Equal(1, gem.DisplayAmount, "GemOfUnity remaining = 1 after Quiet");
            AutoTestAssert.Equal(0, player.Creature.GetPowerAmount<EnergyNextTurnPower>(), "CollectorBadge has not triggered at 2 packs");

            int blockBeforeThird = player.Creature.Block;
            AutoTestAssert.True(batter1.TryManualPlay(enemy), "Play Batter #1 (3rd pack -> triggers BOTH CollectorBadge & GemOfUnity!)");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();

            AutoTestAssert.Equal(3, badge.DisplayAmount, "CollectorBadge reached 3 packs");
            AutoTestAssert.Equal(RelicStatus.Disabled, badge.Status, "CollectorBadge disabled for rest of turn");
            AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnergyNextTurnPower>(), "CollectorBadge applied EnergyNextTurnPower(1)");

            AutoTestAssert.Equal(RelicStatus.Disabled, gem.Status, "GemOfUnity disabled after triggering once");
            AutoTestAssert.True(!gem.ShowCounter, "GemOfUnity hides counter after triggering");
            AutoTestAssert.Equal(blockBeforeThird + 20, player.Creature.Block, "GemOfUnity granted +20 Block");
            AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "GemOfUnity granted +1 Strength");
            AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<DexterityPower>(), "GemOfUnity granted +1 Dexterity");
            AutoTestAssert.Equal(hpBeforeGem + 5, player.Creature.CurrentHp, "GemOfUnity healed +5 HP");

            // 4th distinct pack card (inactive Warlock): CollectorBadge stays capped at 3 (no 2nd EnergyNextTurnPower), GemOfUnity does not re-trigger!
            AutoTestAssert.True(inactiveWarlock.TryManualPlay(null), "Play 4th pack card (Warlock)");
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
            AutoTestAssert.Equal(3, badge.DisplayAmount, "CollectorBadge stayed capped at 3 after 4th pack");
            AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<EnergyNextTurnPower>(), "CollectorBadge did not grant extra EnergyNextTurnPower");
            AutoTestAssert.Equal(1, player.Creature.GetPowerAmount<StrengthPower>(), "GemOfUnity did not re-trigger");

            // Turn 2: Energy = 3 base + 1 BagOfHolding (last turn!) + 1 CollectorBadge = 5 Energy; CollectorBadge reset to 0!
            await EndTurnAndWaitForRoundAsync(player, 2);
            AutoTestAssert.Equal(5, pcs.Energy, "Turn-2 Energy = 3 base + 1 BagOfHolding + 1 CollectorBadge");
            AutoTestAssert.Equal(0, bag.DisplayAmount, "BagOfHolding counter reached 0 on Turn 2");
            AutoTestAssert.True(!bag.ShowCounter, "BagOfHolding hides counter once exhausted");
            AutoTestAssert.Equal(RelicStatus.Disabled, bag.Status, "BagOfHolding disabled after 2 turns");
            AutoTestAssert.Equal(0, badge.DisplayAmount, "CollectorBadge reset to 0 on Turn 2");
            AutoTestAssert.Equal(RelicStatus.Normal, badge.Status, "CollectorBadge re-enabled on Turn 2");

            // Turn 3: BagOfHolding exhausted, no CollectorBadge -> 3 base Energy!
            await EndTurnAndWaitForRoundAsync(player, 3);
            AutoTestAssert.Equal(3, pcs.Energy, "Turn-3 Energy = 3 base (BagOfHolding exhausted after 2 turns)");
        }
        finally
        {
            PackmasterConfig.AllPacksMode = savedAllPacks;
        }
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
