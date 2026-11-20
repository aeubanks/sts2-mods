using System.Reflection;
using BaseLib.Abstracts;
using Downfall.Core;
using Downfall.Hermit;
using Godot;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
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

    internal static IDisposable PreferCards(Func<CardModel, bool> preferred) =>
        new CardSelectorScope(new PredicateCardSelector(preferred));

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
            await NGame.Instance!.GameStartupComplete.WaitAsync(TimeSpan.FromSeconds(45));

            NonInteractiveMode.AutoSlayerCheck = () => true;
            SaveManager.Instance.PrefsSave.FastMode = FastModeType.Instant;

            var filter = System.Environment.GetEnvironmentVariable("DOWNFALL_AUTOTEST_FILTER");
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
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new EndPlayerTurnAction(player, targetRound - 1));
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
        await NGame.Instance!.StartNewSingleplayerRun(
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

        // 3. Combat + lethal damage / player death path.
        CleanupRun();
        var (combatPlayer, _, combatState) = await StartCombatAsync<TCharacter>(seed + "C");
        var creatures = new List<NCreature>();
        CollectNodes(NRun.Instance!, creatures);
        var playerCreatureNode = AutoTestAssert.NotNull(
            creatures.FirstOrDefault(c => c.Entity == combatPlayer.Creature),
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
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), combatPlayer.Creature, 999m, ValueProp.Unblockable | ValueProp.Unpowered, dealer: enemy, cardSource: null, cardPlay: null);
        AutoTestAssert.True(combatPlayer.Creature.IsDead, $"{character.Id} died cleanly from lethal damage");
    }

    private static Task TestBootAndCoexistenceSmoke()
    {
        AutoTestAssert.True(ResourceLoader.Exists("res://Downfall/mod_image.png"), "Downfall mod_image.png packed and loadable");
        AutoTestAssert.NotNull(ResourceLoader.Load<Texture2D>("res://Downfall/mod_image.png"), "Downfall mod_image.png loads as Texture2D");
        AutoTestAssert.True(ModelDb.AllCharacters.Any(), "ModelDb loaded characters cleanly");
        return Task.CompletedTask;
    }

    private static Task TestModelDbAssetsAndLocIntegrity()
    {
        // Enforce global C# class-name uniqueness across all AbstractModel types in Downfall.dll (prevents ModelId collisions).
        var modelTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(AbstractModel).IsAssignableFrom(t))
            .ToList();
        var duplicateNames = modelTypes
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        AutoTestAssert.Equal(0, duplicateNames.Count, $"Duplicate ModelDb class names in Downfall.dll: {string.Join(", ", duplicateNames)}");

        var characters = ModelDb.AllCharacters.OfType<DownfallCharacter>().ToList();
        AutoTestAssert.True(characters.Count >= 1, "At least 1 Downfall character registered");

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

        return Task.CompletedTask;
    }
}
