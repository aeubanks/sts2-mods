using Godot;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.Unlocks;
using Packmaster.Cards;
using Packmaster.Character;

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
    }

    private static Task TestModelDbAssetsAndLocIntegrity()
    {
        var character = ModelDb.Character<ThePackmaster>();
        AutoTestAssert.Equal(75, character.StartingHp, "StartingHp");
        AutoTestAssert.True(character.Title.Exists(), "Character title loc missing");
        AutoTestAssert.True(!string.IsNullOrWhiteSpace(character.Title.GetFormattedText()), "Character title empty");

        AutoTestAssert.NotNull(ModelDb.RelicPool<PackmasterRelicPool>(), "PackmasterRelicPool");
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
        AutoTestAssert.True(cards.Count >= 5, $"Expected at least 5 Packmaster cards, got {cards.Count}");

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

        return Task.CompletedTask;
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
        AutoTestAssert.Equal(2, player.Deck.Cards.Count, "Starter deck size");

        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, MapPointType.Monster, showTransition: false);

        var combatState = AutoTestAssert.NotNull(player.Creature.CombatState, "CombatState after entering monster room");
        await WaitUntilAsync(() =>
            CombatManager.Instance.IsInProgress
            && !CombatManager.Instance.PlayerActionsDisabled
            && player.PlayerCombatState is not null
            && !RunManager.Instance.ActionExecutor.IsRunning);

        var pcs = AutoTestAssert.NotNull(player.PlayerCombatState, "PlayerCombatState");
        var enemy = AutoTestAssert.NotNull(combatState.HittableEnemies.FirstOrDefault(), "First hittable enemy");

        var defend = AutoTestAssert.NotNull(pcs.Hand.Cards.FirstOrDefault(c => c is PackmasterDefend), "PackmasterDefend in hand");
        var energyBeforeDefend = pcs.Energy;
        AutoTestAssert.True(defend.TryManualPlay(null), "Play PackmasterDefend");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(5, player.Creature.Block, "Block after PackmasterDefend");
        AutoTestAssert.Equal(energyBeforeDefend - 1, pcs.Energy, "Energy spent on PackmasterDefend");

        var strike = AutoTestAssert.NotNull(pcs.Hand.Cards.FirstOrDefault(c => c is PackmasterStrike), "PackmasterStrike in hand");
        var enemyHpBefore = enemy.CurrentHp;
        AutoTestAssert.True(strike.TryManualPlay(enemy), "Play PackmasterStrike");
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        AutoTestAssert.Equal(enemyHpBefore - 6, enemy.CurrentHp, "Enemy HP after PackmasterStrike");
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
