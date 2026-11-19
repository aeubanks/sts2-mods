using Godot;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;

namespace Downfall.Testing;

internal static class DownfallAutoTestRunner
{
    private static int _unhandledErrors;

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

    internal static void CleanupRun()
    {
        if (RunManager.Instance.IsInProgress)
        {
            RunManager.Instance.CleanUp(graceful: false);
        }
    }

    private static Task TestBootAndCoexistenceSmoke()
    {
        AutoTestAssert.True(ResourceLoader.Exists("res://Downfall/mod_image.png"), "Downfall mod_image.png packed and loadable");
        AutoTestAssert.NotNull(ResourceLoader.Load<Texture2D>("res://Downfall/mod_image.png"), "Downfall mod_image.png loads as Texture2D");
        AutoTestAssert.True(ModelDb.AllCharacters.Any(), "ModelDb loaded characters cleanly");
        return Task.CompletedTask;
    }
}
