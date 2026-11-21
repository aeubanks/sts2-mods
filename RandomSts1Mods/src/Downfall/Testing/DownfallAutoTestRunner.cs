using System.Diagnostics;
using System.Reflection;
using BaseLib.Abstracts;
using Downfall.Core;
using Downfall.Hermit;
using Godot;
using MegaCrit.Sts2.Core.Assets;
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
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Testing;

internal static class DownfallAutoTestRunner
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

    private sealed class PredicateCardSelector(Func<CardModel, bool> preferred) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var list = options.ToList();
            var matched = list.Where(preferred).Take(maxSelect).ToList();
            if (matched.Count < minSelect)
            {
                matched.AddRange(list.Except(matched).Take(minSelect - matched.Count));
            }
            return Task.FromResult<IEnumerable<CardModel>>(matched);
        }

        public CardRewardSelection GetSelectedCardReward(
            IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) =>
            new AutoSlayCardSelector(new Rng(1)).GetSelectedCardReward(options, alternatives);
    }

    private sealed class CardSelectorScope : IDisposable
    {
        private readonly ICardSelector _previous;

        public CardSelectorScope(ICardSelector next)
        {
            _previous = _activeCardSelector;
            _activeCardSelector = next;
        }

        public void Dispose() => _activeCardSelector = _previous;
    }

    private sealed class CancelSelector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) =>
            Task.FromResult(Enumerable.Empty<CardModel>());

        public CardRewardSelection GetSelectedCardReward(
            IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) =>
            new AutoSlayCardSelector(new Rng(1)).GetSelectedCardReward(options, alternatives);
    }

    internal static IDisposable PreferCards(Func<CardModel, bool> preferred) =>
        new CardSelectorScope(new PredicateCardSelector(preferred));

    internal static IDisposable CancelCardSelection() =>
        new CardSelectorScope(new CancelSelector());

    public static void Start()
    {
        _ = RunAllAsync();
    }

    internal static void Log(string msg)
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
            var suiteTimer = Stopwatch.StartNew();
            await NGame.Instance!.GameStartupComplete.WaitAsync(TimeSpan.FromSeconds(45));
            Log($"Startup ready in {suiteTimer.Elapsed.TotalSeconds:F1}s");

            NonInteractiveMode.AutoSlayerCheck = () => true;
            SaveManager.Instance.PrefsSave.FastMode = FastModeType.Instant;
            SaveManager.Instance.SetFtuesEnabled(false);
            PreloadManager.Enabled = false;

            var filter = System.Environment.GetEnvironmentVariable("DOWNFALL_AUTOTEST_FILTER");
            var shardEnv = System.Environment.GetEnvironmentVariable("DOWNFALL_AUTOTEST_SHARD");
            int shardIndex = 0;
            int shardCount = 1;
            if (!string.IsNullOrEmpty(shardEnv))
            {
                var parts = shardEnv.Split('/');
                if (parts.Length != 2
                    || !int.TryParse(parts[0], out int idx)
                    || !int.TryParse(parts[1], out int cnt)
                    || cnt <= 0 || idx < 0 || idx >= cnt)
                {
                    throw new ArgumentException($"Invalid DOWNFALL_AUTOTEST_SHARD='{shardEnv}' (expected 0-indexed 'i/N')");
                }
                shardIndex = idx;
                shardCount = cnt;
            }

            (string Name, Func<Task> Run)[] tests =
            [
                ("BootAndCoexistenceSmoke", TestBootAndCoexistenceSmoke),
                ("ModelDbAssetsAndLocIntegrity", TestModelDbAssetsAndLocIntegrity),
                ("HermitVisualsRuntime", HermitTests.TestVisualsRuntime),
                ("HermitStarterCombat", HermitTests.TestStarterCombat),
                ("HermitCommonCardsInCombat", HermitTests.TestCommonCardsInCombat),
                ("HermitUncommonBatch1InCombat", HermitTests.TestUncommonBatch1InCombat),
                ("HermitUncommonBatch2InCombat", HermitTests.TestUncommonBatch2InCombat),
                ("HermitRareCardsInCombat", HermitTests.TestRareCardsInCombat),
                ("HermitRelicsAndPotionsInCombat", HermitTests.TestRelicsAndPotionsInCombat),
                ("AwakenedVisualsRuntime", AwakenedTests.TestVisualsRuntime),
                ("AwakenedStarterCombat", AwakenedTests.TestStarterCombat),
                ("AwakenedCommonCardsInCombat", AwakenedTests.TestCommonCardsInCombat),
                ("AwakenedUncommonBatch1InCombat", AwakenedTests.TestUncommonBatch1InCombat),
                ("AwakenedUncommonBatch2InCombat", AwakenedTests.TestUncommonBatch2InCombat),
                ("AwakenedRareCardsInCombat", AwakenedTests.TestRareCardsInCombat),
                ("AwakenedRelicsAndPotionsInCombat", AwakenedTests.TestRelicsAndPotionsInCombat),
                ("GremlinVisualsRuntime", GremlinTests.TestVisualsRuntime),
                ("GremlinStarterCombat", GremlinTests.TestStarterCombat),
                ("GremlinCommonCardsInCombat", GremlinTests.TestCommonCardsInCombat),
                ("GremlinUncommonBatch1InCombat", GremlinTests.TestUncommonBatch1InCombat),
                ("GremlinUncommonBatch2InCombat", GremlinTests.TestUncommonBatch2InCombat),
                ("GremlinRareCardsInCombat", GremlinTests.TestRareCardsInCombat),
                ("GremlinRelicsAndPotionsInCombat", GremlinTests.TestRelicsAndPotionsInCombat),
                ("ChampVisualsRuntime", ChampTests.TestVisualsRuntime),
                ("ChampStarterCombat", ChampTests.TestStarterCombat),
                ("ChampCommonCardsInCombat", ChampTests.TestCommonCardsInCombat),
                ("ChampUncommonBatch1InCombat", ChampTests.TestUncommonBatch1InCombat),
                ("ChampUncommonBatch2InCombat", ChampTests.TestUncommonBatch2InCombat),
                ("ChampRareCardsInCombat", ChampTests.TestRareCardsInCombat),
                ("ChampRelicsAndPotionsInCombat", ChampTests.TestRelicsAndPotionsInCombat),
                ("SneckoVisualsRuntime", SneckoTests.TestVisualsRuntime),
                ("SneckoStarterCombat", SneckoTests.TestStarterCombat),
                ("SneckoCommonCardsInCombat", SneckoTests.TestCommonCardsInCombat),
                ("SneckoUncommonBatch1InCombat", SneckoTests.TestUncommonBatch1InCombat),
                ("SneckoUncommonBatch2InCombat", SneckoTests.TestUncommonBatch2InCombat),
                ("SneckoRareCardsInCombat", SneckoTests.TestRareCardsInCombat),
                ("SneckoRelicsAndPotionsInCombat", SneckoTests.TestRelicsAndPotionsInCombat),
                ("SlimeboundVisualsRuntime", SlimeboundTests.TestVisualsRuntime),
                ("SlimeboundStarterCombat", SlimeboundTests.TestStarterCombat),
                ("SlimeboundCommonCardsInCombat", SlimeboundTests.TestCommonCardsInCombat),
                ("SlimeboundUncommonBatch1InCombat", SlimeboundTests.TestUncommonBatch1AndSpecialistsInCombat),
                ("SlimeboundUncommonBatch2InCombat", SlimeboundTests.TestUncommonBatch2InCombat),
                ("SlimeboundRareCardsInCombat", SlimeboundTests.TestRareCardsInCombat),
                ("SlimeboundRelicsAndPotionsInCombat", SlimeboundTests.TestRelicsAndPotionsInCombat),
                ("GuardianVisualsRuntime", GuardianTests.TestVisualsRuntime),
                ("GuardianStarterGemsStasisAndRestSite", GuardianTests.TestStarterGemsStasisAndRestSite),
                ("GuardianCommonCardsInCombat", GuardianTests.TestCommonCardsInCombat),
                ("GuardianUncommonBatch1InCombat", GuardianTests.TestUncommonBatch1InCombat),
                ("GuardianUncommonBatch2InCombat", GuardianTests.TestUncommonBatch2InCombat),
                ("GuardianRareCardsInCombat", GuardianTests.TestRareCardsInCombat),
                ("GuardianRelicsAndPotionsInCombat", GuardianTests.TestRelicsAndPotionsInCombat),
                ("HexaVisualsRuntime", HexaghostTests.TestVisualsRuntime),
                ("HexaStarterGhostflamesAndAfterlife", HexaghostTests.TestStarterGhostflamesAndAfterlife),
                ("HexaCommonCardsInCombat", HexaghostTests.TestCommonCardsInCombat),
                ("HexaUncommonBatch1InCombat", HexaghostTests.TestUncommonBatch1InCombat),
                ("HexaUncommonBatch2InCombat", HexaghostTests.TestUncommonBatch2InCombat),
                ("HexaRareCardsInCombat", HexaghostTests.TestRareCardsInCombat),
                ("HexaRelicsAndPotionsInCombat", HexaghostTests.TestRelicsAndPotionsInCombat),
                ("AutomatonVisualsRuntime", AutomatonTests.TestVisualsRuntime),
                ("AutomatonStarterEncodeAndFunctionCompile", AutomatonTests.TestStarterEncodeAndFunctionCompile),
                ("AutomatonCommonCardsInCombat", AutomatonTests.TestCommonCardsInCombat),
                ("AutomatonUncommonBatch1InCombat", AutomatonTests.TestUncommonBatch1InCombat),
                ("AutomatonUncommonBatch2InCombat", AutomatonTests.TestUncommonBatch2InCombat),
                ("AutomatonRareCardsInCombat", AutomatonTests.TestRareCardsInCombat),
                ("AutomatonRelicsAndPotionsInCombat", AutomatonTests.TestRelicsAndPotionsInCombat),
                ("CollectorVisualsRuntime", CollectorTests.TestVisualsRuntime),
                ("CollectorStarterMechanicsAndCollection", CollectorTests.TestStarterMechanicsAndCollection),
                ("CollectorCommonCardsInCombat", CollectorTests.TestCommonCardsInCombat),
                ("CollectorUncommonBatch1InCombat", CollectorTests.TestUncommonBatch1InCombat),
                ("CollectorUncommonBatch2InCombat", CollectorTests.TestUncommonBatch2InCombat),
                ("CollectorRareCardsInCombat", CollectorTests.TestRareCardsInCombat),
                ("CollectorRelicsAndPotionsInCombat", CollectorTests.TestRelicsAndPotionsInCombat),
            ];

            int matchedIndex = 0;
            foreach (var (name, run) in tests)
            {
                if (!string.IsNullOrEmpty(filter) && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (matchedIndex++ % shardCount != shardIndex)
                    continue;

                var errorsBefore = Volatile.Read(ref _unhandledErrors);
                var testTimer = Stopwatch.StartNew();
                try
                {
                    _activeCardSelector = new AutoSlayCardSelector(new Rng(1));
                    using var selectorScope = CardSelectorStack.SetTestSelector(new DelegatingCardSelector());
                    Log($"RUN  {name}");
                    await run().WaitAsync(TimeSpan.FromSeconds(45));
                    AutoTestAssert.Equal(errorsBefore, Volatile.Read(ref _unhandledErrors), $"{name} triggered unhandled background exceptions");
                    passed++;
                    Log($"PASS {name} ({testTimer.Elapsed.TotalSeconds:F1}s)");
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"FAIL {name} ({testTimer.Elapsed.TotalSeconds:F1}s): {ex}");
                }
                finally
                {
                    CleanupRun();
                }
            }

            var totalUnhandled = Volatile.Read(ref _unhandledErrors);
            if (failed == 0 && totalUnhandled == 0 && matchedIndex > 0)
            {
                exitCode = 0;
                Log($"SUMMARY: ALL PASSED ({passed}/{passed}) in {suiteTimer.Elapsed.TotalSeconds:F1}s");
            }
            else
            {
                exitCode = 1;
                Log($"SUMMARY: FAILED (passed={passed}, failed={failed}, matched={matchedIndex}, unhandled={totalUnhandled}) in {suiteTimer.Elapsed.TotalSeconds:F1}s");
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

    internal static void CleanupRun()
    {
        if (RunManager.Instance.IsInProgress)
        {
            RunManager.Instance.CleanUp(graceful: false);
        }
    }

    internal static void CollectNodes<T>(Node root, List<T> results) where T : Node
    {
        if (root is T match && !match.IsQueuedForDeletion())
            results.Add(match);
        foreach (var child in root.GetChildren())
            CollectNodes(child, results);
    }

    internal static async Task<(Player Player, RunState RunState, CombatState CombatState)> StartCombatAsync<TCharacter>(string seed)
        where TCharacter : CharacterModel
    {
        var player = Player.CreateForNewRun<TCharacter>(new UnlockState(SaveManager.Instance.Progress), 1uL);
        var runState = await NGame.Instance!.StartNewSingleplayerRun(
            player,
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            isCustomGame: false);

        var encounter = ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.NibbitsWeak>().ToMutable();
        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, encounter, showTransition: false);

        var combatRoom = AutoTestAssert.NotNull(runState.CurrentRoom as CombatRoom, "Entered CombatRoom");
        var combatState = AutoTestAssert.NotNull(combatRoom.CombatState, "CombatState active");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        return (player, runState, combatState);
    }

    internal static async Task EndTurnAndWaitForRoundAsync(Player player, int targetRound)
    {
        var tcs = new TaskCompletionSource();
        void OnTurnStarted(CombatState state)
        {
            if (state.CurrentSide == CombatSide.Player && state.RoundNumber >= targetRound)
                tcs.TrySetResult();
        }

        CombatManager.Instance.TurnStarted += OnTurnStarted;
        try
        {
            int currentRound = player.Creature.CombatState?.RoundNumber ?? (targetRound - 1);
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player, currentRound));
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        }
        finally
        {
            CombatManager.Instance.TurnStarted -= OnTurnStarted;
        }
    }

    internal static async Task RunCharacterVisualsRuntimeTest<TCharacter>(
        string seed,
        Func<string?, bool>? isValidCombatSprite = null)
        where TCharacter : DownfallCharacter
    {
        var character = ModelDb.Character<TCharacter>();
        string spritePath = character.SpriteTexturePath;

        var player = Player.CreateForNewRun<TCharacter>(new UnlockState(SaveManager.Instance.Progress), 1uL);
        var runState = await NGame.Instance!.StartNewSingleplayerRun(
            player,
            shouldSave: false,
            ActModel.GetDefaultList(),
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            isCustomGame: false);

        // 1. Rest Site: _Ready() runs on live NRestSiteCharacter in SceneTree.
        await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, MapPointType.RestSite, null, showTransition: false);
        var restChars = new List<NRestSiteCharacter>();
        CollectNodes(NRun.Instance!, restChars);
        AutoTestAssert.True(restChars.Count >= 1, $"{character.Id} rest site mounted live NRestSiteCharacter");
        var restChar = restChars[0];
        AutoTestAssert.True(restChar.IsNodeReady(), $"{character.Id} NRestSiteCharacter._Ready() completed");
        var restSprite = AutoTestAssert.NotNull(restChar.GetNodeOrNull<Sprite2D>("ControlRoot/Visuals"), $"{character.Id} live rest site Sprite2D");
        AutoTestAssert.Equal(spritePath, restSprite.Texture?.ResourcePath, $"{character.Id} live rest site uses custom sprite");
        AutoTestAssert.NotNull(restChar.Hitbox, $"{character.Id} live rest site Hitbox initialized");

        // 2. Shop: _Ready() and PlayAnimation run cleanly on live NMerchantCharacter.
        // Full RoomType.Shop requires at least one Common/Uncommon/Rare Attack, Skill, and Power in the pool.
        var draftable = character.CardPool.AllCards
            .Where(c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();
        bool canPopulateFullShop =
            draftable.Any(c => c.Type == CardType.Attack)
            && draftable.Any(c => c.Type == CardType.Skill)
            && draftable.Any(c => c.Type == CardType.Power);

        if (canPopulateFullShop)
        {
            await RunManager.Instance.EnterRoomDebug(RoomType.Shop, MapPointType.Shop, null, showTransition: false);
            var merchantChars = new List<NMerchantCharacter>();
            CollectNodes(NRun.Instance!, merchantChars);
            AutoTestAssert.True(merchantChars.Count >= 1, $"{character.Id} shop mounted live NMerchantCharacter");
            var merchantChar = merchantChars[0];
            AutoTestAssert.True(merchantChar.IsNodeReady(), $"{character.Id} NMerchantCharacter._Ready() completed");
            var merchantSprite = AutoTestAssert.NotNull(merchantChar.GetNodeOrNull<Sprite2D>("%Visuals"), $"{character.Id} live merchant %Visuals Sprite2D");
            AutoTestAssert.Equal(spritePath, merchantSprite.Texture?.ResourcePath, $"{character.Id} live merchant uses custom sprite");
            merchantChar.PlayAnimation("relaxed_loop", loop: true);
        }
        else
        {
            var merchantChar = AutoTestAssert.NotNull(
                ResourceLoader.Load<PackedScene>(character.MerchantAnimPath)?.Instantiate<NMerchantCharacter>(),
                $"{character.Id} instantiated NMerchantCharacter");
            NRun.Instance!.AddChild(merchantChar);
            try
            {
                AutoTestAssert.True(merchantChar.IsNodeReady(), $"{character.Id} NMerchantCharacter._Ready() completed");
                var merchantSprite = AutoTestAssert.NotNull(merchantChar.GetNodeOrNull<Sprite2D>("%Visuals"), $"{character.Id} mounted merchant %Visuals Sprite2D");
                AutoTestAssert.Equal(spritePath, merchantSprite.Texture?.ResourcePath, $"{character.Id} mounted merchant uses custom sprite");
                merchantChar.PlayAnimation("relaxed_loop", loop: true);
            }
            finally
            {
                NRun.Instance!.RemoveChild(merchantChar);
                merchantChar.Free();
            }
        }

        // 3. Combat + lethal damage / player death path (reuse the same RunState — no second StartNewSingleplayerRun!).
        var encounter = ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.NibbitsWeak>().ToMutable();
        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, encounter, showTransition: false);
        var combatRoom = AutoTestAssert.NotNull(runState.CurrentRoom as CombatRoom, "Entered CombatRoom");
        var combatState = AutoTestAssert.NotNull(combatRoom.CombatState, "CombatState active");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        var creatures = new List<NCreature>();
        CollectNodes(NRun.Instance!, creatures);
        var playerCreatureNode = AutoTestAssert.NotNull(
            creatures.FirstOrDefault(c => c.Entity == player.Creature),
            $"{character.Id} live player NCreature mounted in combat");
        AutoTestAssert.True(playerCreatureNode.Visuals.IsNodeReady(), $"{character.Id} NCreatureVisuals._Ready() completed");
        var combatSprite = AutoTestAssert.NotNull(playerCreatureNode.Visuals.Body as Sprite2D, $"{character.Id} live combat body is Sprite2D");
        if (isValidCombatSprite is not null)
        {
            AutoTestAssert.True(isValidCombatSprite(combatSprite.Texture?.ResourcePath),
                $"{character.Id} live combat uses valid custom sprite (got {combatSprite.Texture?.ResourcePath})");
        }
        else
        {
            AutoTestAssert.Equal(spritePath, combatSprite.Texture?.ResourcePath, $"{character.Id} live combat uses custom sprite");
        }
        AutoTestAssert.Equal(Control.MouseFilterEnum.Ignore, playerCreatureNode.Visuals.Bounds.MouseFilter, $"{character.Id} combat %Bounds ignores mouse");
        AutoTestAssert.True(playerCreatureNode.Visuals.VfxSpawnPosition.Position.Y < -50f,
            $"{character.Id} VfxSpawnPosition should be on torso (got Y={playerCreatureNode.Visuals.VfxSpawnPosition.Position.Y})");

        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "Enemy for lethal test");
        for (int attempt = 0; attempt < 6 && player.Creature.IsAlive; attempt++)
        {
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), player.Creature, 999m, ValueProp.Unblockable | ValueProp.Unpowered, dealer: enemy, cardSource: null, cardPlay: null);
        }
        AutoTestAssert.True(player.Creature.IsDead, $"{character.Id} died cleanly from lethal damage");
    }

    private static Task TestBootAndCoexistenceSmoke()
    {
        AutoTestAssert.True(ResourceLoader.Exists("res://RandomSts1Mods/mod_image.png"), "RandomSts1Mods mod_image.png packed and loadable");
        AutoTestAssert.NotNull(ResourceLoader.Load<Texture2D>("res://RandomSts1Mods/mod_image.png"), "RandomSts1Mods mod_image.png loads as Texture2D");
        AutoTestAssert.True(ResourceLoader.Exists("res://Downfall/mod_image.png"), "Downfall mod_image.png packed and loadable");
        AutoTestAssert.NotNull(ResourceLoader.Load<Texture2D>("res://Downfall/mod_image.png"), "Downfall mod_image.png loads as Texture2D");
        AutoTestAssert.True(ModelDb.AllCharacters.Any(), "ModelDb loaded characters cleanly");
        return Task.CompletedTask;
    }

    private static Task TestModelDbAssetsAndLocIntegrity()
    {
        // Enforce ModelId uniqueness across all AbstractModel types in the merged assembly (BaseLib prefixes by root namespace).
        var modelTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(AbstractModel).IsAssignableFrom(t))
            .ToList();
        var duplicateNames = modelTypes
            .GroupBy(t => $"{BaseLib.Extensions.TypePrefix.GetPrefix(t)}{t.Name}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        AutoTestAssert.Equal(0, duplicateNames.Count, $"Duplicate prefixed ModelDb names in assembly: {string.Join(", ", duplicateNames)}");

        var characters = ModelDb.AllCharacters.OfType<DownfallCharacter>().ToList();
        AutoTestAssert.True(characters.Count >= 1, "At least 1 Downfall character registered");

        static void ValidateCard(CardModel card)
        {
            AutoTestAssert.True(ResourceLoader.Exists(card.PortraitPath), $"Missing portrait for {card.Id}: {card.PortraitPath}");
            if (card is DownfallCard dfCard)
                AutoTestAssert.True(ResourceLoader.Exists(dfCard.CustomPortraitPath), $"Missing big portrait for {card.Id}: {dfCard.CustomPortraitPath}");

            AutoTestAssert.True(new LocString("cards", $"{card.Id.Entry}.title").Exists(), $"Missing title loc for {card.Id}");
            AutoTestAssert.True(card.Description.Exists(), $"Missing description loc for {card.Id}");

            var desc = card.GetDescriptionForPile(PileType.None);
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(desc) && !desc.Contains('{') && !desc.Contains('}'),
                $"Invalid description for {card.Id}: {desc}");

            var upgDesc = card.GetDescriptionForUpgradePreview();
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(upgDesc) && !upgDesc.Contains('{') && !upgDesc.Contains('}'),
                $"Invalid upgrade description for {card.Id}+: {upgDesc}");

            // Materialize HoverTips (base + upgraded) to catch broken or cyclic card/power tips.
            _ = card.HoverTips.ToList();
            if (card.IsUpgradable)
            {
                var upgClone = (CardModel)card.MutableClone();
                upgClone.UpgradeInternal();
                upgClone.FinalizeUpgradeInternal();
                _ = upgClone.HoverTips.ToList();
            }
        }

        foreach (var character in characters)
        {
            AutoTestAssert.True(character.Title.Exists(), $"Missing title loc for {character.Id}");
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(character.Title.GetFormattedText()), $"Empty title for {character.Id}");

            string[] charAssets =
            [
                character.SpriteTexturePath,
                character.CustomVisualPath,
                character.CustomRestSiteAnimPath,
                character.CustomMerchantAnimPath,
                character.CustomIconTexturePath,
                character.CustomIconOutlineTexturePath,
                character.CustomCharacterSelectIconPath,
                character.CustomCharacterSelectLockedIconPath,
                character.CustomCharacterSelectBg,
                character.CustomMapMarkerPath,
            ];
            foreach (var path in charAssets)
                AutoTestAssert.True(ResourceLoader.Exists(path), $"Missing character asset for {character.Id}: {path}");

            AutoTestAssert.NotNull(ResourceLoader.Load<Texture2D>(character.SpriteTexturePath), $"Failed to load sprite Texture2D for {character.Id}");
            AutoTestAssert.NotNull(ResourceLoader.Load<Texture2D>(character.CustomIconOutlineTexturePath), $"Failed to load outline Texture2D for {character.Id}");

            CharacterModel baseChar = character;
            AutoTestAssert.Equal(character.CustomRestSiteAnimPath, baseChar.RestSiteAnimPath, $"{character.Id} RestSiteAnimPath resolves");
            AutoTestAssert.Equal(character.CustomMerchantAnimPath, baseChar.MerchantAnimPath, $"{character.Id} MerchantAnimPath resolves");
            AutoTestAssert.Equal(character.CustomIconOutlineTexturePath, baseChar.IconOutlineTexture?.ResourcePath, $"{character.Id} IconOutlineTexture resolves");

            // Validate card pool + cards
            var cardPool = character.CardPool;
            if (cardPool is CustomCardPoolModel customPool)
            {
                AutoTestAssert.True(ResourceLoader.Exists(customPool.BigEnergyIconPath), $"Missing {customPool.BigEnergyIconPath}");
                AutoTestAssert.True(ResourceLoader.Exists(customPool.TextEnergyIconPath), $"Missing {customPool.TextEnergyIconPath}");
            }

            foreach (var card in cardPool.AllCards)
                ValidateCard(card);

            // Validate relic pool + relics
            foreach (var relic in character.RelicPool.AllRelics)
            {
                if (relic is DownfallRelic dfRelic)
                {
                    foreach (var iconPath in dfRelic.AllIconPaths)
                        AutoTestAssert.True(ResourceLoader.Exists(iconPath), $"Missing relic asset for {relic.Id}: {iconPath}");
                }
                AutoTestAssert.True(relic.Title.Exists(), $"Missing relic title loc for {relic.Id}");
                AutoTestAssert.True(relic.DynamicDescription.Exists(), $"Missing relic desc loc for {relic.Id}");
                AutoTestAssert.True(relic.Flavor.Exists(), $"Missing relic flavor loc for {relic.Id}");
                var rDesc = relic.DynamicDescription.GetFormattedText();
                AutoTestAssert.True(!string.IsNullOrWhiteSpace(rDesc) && !rDesc.Contains('{') && !rDesc.Contains('}'),
                    $"Invalid relic description for {relic.Id}: {rDesc}");
            }

            // Validate potion pool + potions
            foreach (var potion in character.PotionPool.AllPotions)
            {
                AutoTestAssert.True(ResourceLoader.Exists(potion.ImagePath), $"Missing potion image for {potion.Id}: {potion.ImagePath}");
                AutoTestAssert.True(potion.OutlinePath is not null && ResourceLoader.Exists(potion.OutlinePath), $"Missing potion outline for {potion.Id}");
                AutoTestAssert.True(potion.Title.Exists(), $"Missing potion title loc for {potion.Id}");
                AutoTestAssert.True(potion.DynamicDescription.Exists(), $"Missing potion desc loc for {potion.Id}");
                var pDesc = potion.DynamicDescription.GetFormattedText();
                AutoTestAssert.True(!string.IsNullOrWhiteSpace(pDesc) && !pDesc.Contains('{') && !pDesc.Contains('}'),
                    $"Invalid potion description for {potion.Id}: {pDesc}");
            }
        }

        // Validate all Downfall custom TokenCardPool cards (Lick, SpecialistChoiceCards, etc.).
        var tokenCards = ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.TokenCardPool>()
            .AllCards
            .OfType<DownfallCard>()
            .ToList();
        AutoTestAssert.True(tokenCards.Count >= 1, "At least 1 Downfall token card registered");
        foreach (var tokenCard in tokenCards)
            ValidateCard(tokenCard);

        // Validate all Downfall custom powers.
        var customPowers = ModelDb.AllPowers.OfType<DownfallPower>().ToList();
        AutoTestAssert.True(customPowers.Count >= 3, $"Expected at least 3 Downfall powers, got {customPowers.Count}");
        foreach (var power in customPowers)
        {
            foreach (var iconPath in power.AllIconPaths)
                AutoTestAssert.True(ResourceLoader.Exists(iconPath), $"Missing power asset for {power.Id}: {iconPath}");
            AutoTestAssert.True(power.Title.Exists(), $"Missing power title loc for {power.Id}");
            AutoTestAssert.True(power.Description.Exists(), $"Missing power desc loc for {power.Id}");
            AutoTestAssert.True(power.SmartDescription.Exists(), $"Missing power smartDescription loc for {power.Id}");
            foreach (int testAmount in new[] { 1, 2 })
            {
                var smartLoc = power.SmartDescription;
                power.DynamicVars.AddTo(smartLoc);
                smartLoc.Add("Amount", testAmount);
                smartLoc.Add("OnPlayer", true);
                smartLoc.Add("IsMultiplayer", false);
                smartLoc.Add("PlayerCount", 1);
                smartLoc.Add("OwnerName", "Hermit");
                var smartText = smartLoc.GetFormattedText();
                AutoTestAssert.True(!string.IsNullOrWhiteSpace(smartText) && !smartText.Contains('{') && !smartText.Contains('}'),
                    $"Invalid smartDescription for {power.Id} (Amount={testAmount}): {smartText}");
            }
            _ = power.HoverTips.ToList();
        }

        // Validate all Downfall custom orbs.
        var customOrbs = modelTypes
            .Where(t => typeof(DownfallOrb).IsAssignableFrom(t))
            .Select(t => ModelDb.DebugOrb(t))
            .OfType<DownfallOrb>()
            .ToList();
        AutoTestAssert.True(customOrbs.Count >= 4, $"Expected at least 4 Downfall orbs, got {customOrbs.Count}");
        foreach (var orb in customOrbs)
        {
            AutoTestAssert.True(ResourceLoader.Exists(orb.CustomIconPath), $"Missing orb icon for {orb.Id}: {orb.CustomIconPath}");
            AutoTestAssert.True(orb.Title.Exists(), $"Missing orb title loc for {orb.Id}");
            AutoTestAssert.True(orb.Description.Exists(), $"Missing orb desc loc for {orb.Id}");
            AutoTestAssert.True(orb.SmartDescription.Exists(), $"Missing orb smartDescription loc for {orb.Id}");
            var desc = orb.Description.GetFormattedText();
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(desc) && !desc.Contains('{') && !desc.Contains('}'),
                $"Invalid orb description for {orb.Id}: {desc}");
            var smartLoc = orb.SmartDescription;
            smartLoc.Add("Passive", orb.PassiveVal);
            smartLoc.Add("Evoke", orb.EvokeVal);
            var smartText = smartLoc.GetFormattedText();
            AutoTestAssert.True(!string.IsNullOrWhiteSpace(smartText) && !smartText.Contains('{') && !smartText.Contains('}'),
                $"Invalid orb smartDescription for {orb.Id}: {smartText}");
            _ = orb.HoverTips.ToList();
        }

        // Validate character-specific gameplay UI and prompt loc keys.
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-DEAD_ON_KEYWORD.title").Exists(), "Missing Dead On title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-DEAD_ON_KEYWORD.description").Exists(), "Missing Dead On desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-COALESCENCE.selectionScreenPrompt").Exists(), "Missing Coalescence prompt loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-ADAPT.selectionScreenPrompt").Exists(), "Missing Adapt prompt loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-CHANT_KEYWORD.title").Exists(), "Missing Chant title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-CHANT_KEYWORD.description").Exists(), "Missing Chant desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-CONJURE_KEYWORD.title").Exists(), "Missing Conjure title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-CONJURE_KEYWORD.description").Exists(), "Missing Conjure desc loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-COVET.selectionScreenPrompt").Exists(), "Missing Covet selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-BODY_ARMOR.selectionScreenPrompt").Exists(), "Missing BodyArmor selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-CHEAT.selectionScreenPrompt").Exists(), "Missing Cheat selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-LONE_WOLF.selectionScreenPrompt").Exists(), "Missing LoneWolf selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-MALICE.selectionScreenPrompt").Exists(), "Missing Malice selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-DEJECTION.selectionScreenPrompt").Exists(), "Missing Dejection selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-GATHER.selectionScreenPrompt").Exists(), "Missing Gather selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-PROFANE_STRIKE.selectionScreenPrompt").Exists(), "Missing ProfaneStrike selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-ALTAR.selectionScreenPrompt").Exists(), "Missing Altar selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-BIRDS_EYE.selectionScreenPrompt").Exists(), "Missing BirdsEye selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-INSCRIBE.selectionScreenPrompt").Exists(), "Missing Inscribe selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-MIDDEN_HEAP.selectionScreenPrompt").Exists(), "Missing MiddenHeap selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-FOURTH_DIMENSION.selectionScreenPrompt").Exists(), "Missing FourthDimension selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-PROCESSION.selectionScreenPrompt").Exists(), "Missing Procession selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("potions", "DOWNFALL-ECLIPSE.selectionScreenPrompt").Exists(), "Missing Eclipse selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-MOON_TALISMAN.selectionScreenPrompt").Exists(), "Missing MoonTalisman selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-SWAP_KEYWORD.title").Exists(), "Missing Swap title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-SWAP_KEYWORD.description").Exists(), "Missing Swap desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-TEMP_HP_KEYWORD.title").Exists(), "Missing Temp HP title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-TEMP_HP_KEYWORD.description").Exists(), "Missing Temp HP desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-ECHO_KEYWORD.title").Exists(), "Missing Echo title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-ECHO_KEYWORD.description").Exists(), "Missing Echo desc loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-TAG_TEAM.selectionScreenPrompt").Exists(), "Missing TagTeam selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-RHYTHM.selectionScreenPrompt").Exists(), "Missing Rhythm selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-DUPLICATE.selectionScreenPrompt").Exists(), "Missing Duplicate selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-BERSERKER_STANCE_KEYWORD.title").Exists(), "Missing Berserker Stance title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-BERSERKER_STANCE_KEYWORD.description").Exists(), "Missing Berserker Stance desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-DEFENSIVE_STANCE_KEYWORD.title").Exists(), "Missing Defensive Stance title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-DEFENSIVE_STANCE_KEYWORD.description").Exists(), "Missing Defensive Stance desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-ULTIMATE_STANCE_KEYWORD.title").Exists(), "Missing Ultimate Stance title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-ULTIMATE_STANCE_KEYWORD.description").Exists(), "Missing Ultimate Stance desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-FINISHER_KEYWORD.title").Exists(), "Missing Finisher title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-FINISHER_KEYWORD.description").Exists(), "Missing Finisher desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-COMBO_KEYWORD.title").Exists(), "Missing Combo title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-COMBO_KEYWORD.description").Exists(), "Missing Combo desc loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-CHAMP_HEADBUTT.selectionScreenPrompt").Exists(), "Missing ChampHeadbutt selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-WIND_UP.selectionScreenPrompt").Exists(), "Missing WindUp selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-ENCHANT_CROWN.selectionScreenPrompt").Exists(), "Missing EnchantCrown selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-ENCHANT_SHIELD.selectionScreenPrompt").Exists(), "Missing EnchantShield selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-ENCHANT_SWORD.selectionScreenPrompt").Exists(), "Missing EnchantSword selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-SIGNATURE_FINISHER.selectionScreenPrompt").Exists(), "Missing SignatureFinisher selectionScreenPrompt loc");
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-GREMLIN_MOB_STATUS.title").Exists(), "Missing Gremlin Mob Status title loc");
        AutoTestAssert.True(new LocString("static_hover_tips", "HIT_POINTS.title").Exists(), "Missing static HIT_POINTS.title loc");
        AutoTestAssert.True(new LocString("static_hover_tips", "HIT_POINTS.description").Exists(), "Missing static HIT_POINTS.description loc");
        foreach (var ch in characters)
        {
            AutoTestAssert.True(new LocString("relics", $"SEA_GLASS.{ch.Id.Entry}.title").Exists(), $"Missing Sea Glass title loc for {ch.Id.Entry}");
            foreach (var starter in ch.StartingRelics.OfType<CustomRelicModel>())
            {
                var upg = AutoTestAssert.NotNull(starter.GetUpgradeReplacement(), $"{starter.Id} defines Orobas GetUpgradeReplacement()");
                AutoTestAssert.Equal(RelicRarity.Starter, upg.Rarity, $"{upg.Id} uses RelicRarity.Starter so it is excluded from normal relic pools");
            }
        }
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-GREMLIN_KNOB.additionalRestSiteHealText").Exists(), "Missing GremlinKnob additionalRestSiteHealText loc");
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-GREMLIN_KNOB_UPGRADE.additionalRestSiteHealText").Exists(), "Missing GremlinKnobUpgrade additionalRestSiteHealText loc");
        AutoTestAssert.True(ResourceLoader.Exists("res://Downfall/images/champ/ui/stance_charge_on.png"), "Missing Champ stance_charge_on.png");
        AutoTestAssert.True(ResourceLoader.Exists("res://Downfall/images/champ/ui/stance_charge_off.png"), "Missing Champ stance_charge_off.png");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-MUDDLE_KEYWORD.title").Exists(), "Missing Muddle title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-MUDDLE_KEYWORD.description").Exists(), "Missing Muddle desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-OVERFLOW_KEYWORD.title").Exists(), "Missing Overflow title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-OVERFLOW_KEYWORD.description").Exists(), "Missing Overflow desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-OFFCLASS_KEYWORD.title").Exists(), "Missing Offclass title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-OFFCLASS_KEYWORD.description").Exists(), "Missing Offclass desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-GIFT_KEYWORD.title").Exists(), "Missing Gift title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-GIFT_KEYWORD.description").Exists(), "Missing Gift desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-LUCKY_KEYWORD.title").Exists(), "Missing Lucky title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-LUCKY_KEYWORD.description").Exists(), "Missing Lucky desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-SNECKO_MUDDLE.selectionScreenPrompt").Exists(), "Missing Snecko Muddle prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-CRYSTAL_BOOMERANG.selectionScreenPrompt").Exists(), "Missing CrystalBoomerang prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-NOPE.selectionScreenPrompt").Exists(), "Missing Nope prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-ROUNDABOUT_SWING.selectionScreenPrompt").Exists(), "Missing RoundaboutSwing prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-TRASH_TO_TREASURE.selectionScreenPrompt").Exists(), "Missing TrashToTreasure prompt loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-TRASH_CAN.selectionScreenPrompt").Exists(), "Missing TrashCan prompt loc");
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-SEAL_OF_APPROVAL.selectionScreenPrompt").Exists(), "Missing SealOfApproval prompt loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-SPLIT_KEYWORD.title").Exists(), "Missing Split title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-SPLIT_KEYWORD.description").Exists(), "Missing Split desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-COMMAND_KEYWORD.title").Exists(), "Missing Command title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-COMMAND_KEYWORD.description").Exists(), "Missing Command desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-ABSORB_KEYWORD.title").Exists(), "Missing Absorb title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-ABSORB_KEYWORD.description").Exists(), "Missing Absorb desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-TACKLE_KEYWORD.title").Exists(), "Missing Tackle title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-TACKLE_KEYWORD.description").Exists(), "Missing Tackle desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-CONSUME_KEYWORD.title").Exists(), "Missing Consume title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-CONSUME_KEYWORD.description").Exists(), "Missing Consume desc loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-SELF_FORMING_GOO.selectionScreenPrompt").Exists(), "Missing SelfFormingGoo prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-SLIMEBOUND_DISSOLVE.selectionScreenPrompt").Exists(), "Missing SlimeboundDissolve prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-REPLICATION.selectionScreenPrompt").Exists(), "Missing Replication prompt loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-BRACE_KEYWORD.title").Exists(), "Missing Brace title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-BRACE_KEYWORD.description").Exists(), "Missing Brace desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-STASIS_KEYWORD.title").Exists(), "Missing Stasis title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-STASIS_KEYWORD.description").Exists(), "Missing Stasis desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-ACCELERATE_KEYWORD.title").Exists(), "Missing Accelerate title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-ACCELERATE_KEYWORD.description").Exists(), "Missing Accelerate desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-VOLATILE_KEYWORD.title").Exists(), "Missing Volatile title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-VOLATILE_KEYWORD.description").Exists(), "Missing Volatile desc loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-GEM_KEYWORD.title").Exists(), "Missing Gem title loc");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-GEM_KEYWORD.description").Exists(), "Missing Gem desc loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-CURL_UP.selectionScreenPrompt").Exists(), "Missing CurlUp prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-SUSPENSION.selectionScreenPrompt").Exists(), "Missing Suspension prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-CLONE.selectionScreenPrompt").Exists(), "Missing Clone prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-PREPROGRAM.selectionScreenPrompt").Exists(), "Missing Preprogram prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-RECOVER.selectionScreenPrompt").Exists(), "Missing Recover prompt loc");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-FUTURE_PLANS.selectionScreenPrompt").Exists(), "Missing FuturePlans prompt loc");
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-BOTTLED_STASIS.selectionScreenPrompt").Exists(), "Missing BottledStasis prompt loc");
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-BOTTLED_ANOMALY.selectionScreenPrompt").Exists(), "Missing BottledAnomaly prompt loc");
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-SACK_OF_GEMS.selectionScreenPrompt").Exists(), "Missing SackOfGems prompt loc");
        AutoTestAssert.True(new LocString("rest_site_ui", "OPTION_DOWNFALL_SOCKET_GEM.name").Exists(), "Missing SocketGem rest site name loc");
        AutoTestAssert.True(new LocString("rest_site_ui", "OPTION_DOWNFALL_SOCKET_GEM.description").Exists(), "Missing SocketGem rest site desc loc");
        AutoTestAssert.True(new LocString("rest_site_ui", "OPTION_DOWNFALL_SOCKET_GEM.descriptionDisabled").Exists(), "Missing SocketGem rest site disabled desc loc");
        AutoTestAssert.True(new LocString("rest_site_ui", "OPTION_DOWNFALL_SOCKET_GEM.selectGemPrompt").Exists(), "Missing SocketGem selectGemPrompt loc");
        AutoTestAssert.True(new LocString("rest_site_ui", "OPTION_DOWNFALL_SOCKET_GEM.selectTargetPrompt").Exists(), "Missing SocketGem selectTargetPrompt loc");
        AutoTestAssert.True(ResourceLoader.Exists("res://images/ui/rest_site/option_downfall_socket_gem.png"), "Missing SocketGem rest site icon");
        AutoTestAssert.True(ResourceLoader.Exists(Guardian.GuardianMechanics.DefensiveSpritePath), "Missing Guardian defensive sprite");
        foreach (var flameOrb in new[]
        {
            "searing_ghostflame_orb",
            "crushing_ghostflame_orb",
            "bolstering_ghostflame_orb",
            "inferno_ghostflame_orb"
        })
        {
            AutoTestAssert.True(
                ResourceLoader.Exists($"res://Downfall/images/hexaghost/orbs/{flameOrb}_bright.png"),
                $"Missing Hexaghost bright orb texture: {flameOrb}_bright.png");
        }
        foreach (var hexaTipKey in new[]
        {
            "DOWNFALL-HEXA_AFTERLIFE",
            "DOWNFALL-HEXA_GHOSTFLAME",
            "DOWNFALL-HEXA_IGNITE",
            "DOWNFALL-HEXA_ADVANCE",
            "DOWNFALL-HEXA_RETRACT",
            "DOWNFALL-HEXA_EXTINGUISH"
        })
        {
            AutoTestAssert.True(new LocString("gameplay_ui", $"{hexaTipKey}.title").Exists(), $"Missing {hexaTipKey}.title");
            AutoTestAssert.True(new LocString("gameplay_ui", $"{hexaTipKey}.description").Exists(), $"Missing {hexaTipKey}.description");
        }
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-SHIELD_OF_DARKNESS.selectionScreenPrompt").Exists(), "Missing ShieldOfDarkness prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-SWORD_OF_DARKNESS.selectionScreenPrompt").Exists(), "Missing SwordOfDarkness prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-NECESSARY_SACRIFICE.selectionScreenPrompt").Exists(), "Missing NecessarySacrifice prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-RECURRING_NIGHTMARE.selectionScreenPrompt").Exists(), "Missing RecurringNightmare prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-REWIND.selectionScreenPrompt").Exists(), "Missing Rewind prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-SEVENTH_EYE.selectionScreenPrompt").Exists(), "Missing SeventhEye prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-SPECTRAL_GRACE.selectionScreenPrompt").Exists(), "Missing SpectralGrace prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-ETHER_STEP.selectionScreenPrompt").Exists(), "Missing EtherStep prompt");
        foreach (var bronzeTipKey in new[]
        {
            "DOWNFALL-BRONZE_ENCODE",
            "DOWNFALL-BRONZE_COMPILE",
            "DOWNFALL-BRONZE_INSERT",
            "DOWNFALL-BRONZE_CYCLE",
            "DOWNFALL-BRONZE_BLUR"
        })
        {
            AutoTestAssert.True(new LocString("gameplay_ui", $"{bronzeTipKey}.title").Exists(), $"Missing {bronzeTipKey}.title");
            AutoTestAssert.True(new LocString("gameplay_ui", $"{bronzeTipKey}.description").Exists(), $"Missing {bronzeTipKey}.description");
        }
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-BIT_SHIFT.selectionScreenPrompt").Exists(), "Missing BitShift prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-CUT_THROUGH.selectionScreenPrompt").Exists(), "Missing CutThrough prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-REFACTOR.selectionScreenPrompt").Exists(), "Missing Refactor prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-BRONZE_RETURN.selectionScreenPrompt").Exists(), "Missing BronzeReturn prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-ASSEMBLY.selectionScreenPrompt").Exists(), "Missing Assembly prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-FIND_AND_REPLACE.selectionScreenPrompt").Exists(), "Missing FindAndReplace prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-MUTATOR.selectionScreenPrompt").Exists(), "Missing Mutator prompt");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-TINKERERS_TOOLBOX.selectionScreenPrompt").Exists(), "Missing TinkerersToolbox prompt");
        foreach (var collectorTipKey in new[]
        {
            "DOWNFALL-COLLECTOR_PYRE",
            "DOWNFALL-COLLECTOR_AFFLICTED",
            "DOWNFALL-COLLECTOR_COLLECTED",
            "DOWNFALL-COLLECTOR_TORCHHEAD",
            "DOWNFALL-COLLECTOR_COLLECTION_STATUS"
        })
        {
            AutoTestAssert.True(new LocString("gameplay_ui", $"{collectorTipKey}.title").Exists(), $"Missing {collectorTipKey}.title");
            AutoTestAssert.True(new LocString("gameplay_ui", $"{collectorTipKey}.description").Exists(), $"Missing {collectorTipKey}.description");
        }
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-COLLECTOR_PYRE.selectPrompt").Exists(), "Missing Collector Pyre selectPrompt");
        AutoTestAssert.True(new LocString("gameplay_ui", "DOWNFALL-COLLECTIBLE_REWARD.description").Exists(), "Missing CollectibleReward description");
        AutoTestAssert.True(new LocString("cards", "DOWNFALL-WHIRLING_FLAME.selectionScreenPrompt").Exists(), "Missing WhirlingFlame selectionScreenPrompt");
        AutoTestAssert.True(new LocString("relics", "DOWNFALL-BOTTLED_COLLECTIBLE.selectPrompt").Exists(), "Missing BottledCollectible selectPrompt");
        AutoTestAssert.True(new LocString("rest_site_ui", "OPTION_DOWNFALL_RELEASE.name").Exists(), "Missing Release rest site name");
        AutoTestAssert.True(new LocString("rest_site_ui", "OPTION_DOWNFALL_RELEASE.description").Exists(), "Missing Release rest site description");
        AutoTestAssert.True(new LocString("rest_site_ui", "OPTION_DOWNFALL_RELEASE.descriptionDisabled").Exists(), "Missing Release rest site descriptionDisabled");
        AutoTestAssert.True(new LocString("rest_site_ui", "OPTION_DOWNFALL_RELEASE.selectPrompt").Exists(), "Missing Release rest site selectPrompt");
        AutoTestAssert.True(ResourceLoader.Exists("res://images/ui/rest_site/option_downfall_release.png"), "Missing Release rest site icon");

        return Task.CompletedTask;
    }
}
