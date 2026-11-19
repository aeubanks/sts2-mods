# Packmaster StS2 — Automated Testing Plan

## Goal
Run fast, zero-interaction, deterministic automated tests for the Packmaster mod after every change — verifying compilation, Godot asset packing, SmartFormat localization, pack/card-pool logic, and real in-engine run/combat execution — without touching the user's real Steam save files or opening a game window.

## Architecture (Single Layer, Zero Mocks, Zero Test Harmony Patches)
We run tests **directly inside the real game engine in headless mode**:

```text
scripts/test.sh [--pack]
  1. Ensure isolated headless profile (~/.local/share/SlayTheSpire2/default/1/settings.save)
     has `mod_settings.mods_enabled = true`, `seen_ea_disclaimer = true`,
     `skip_intro_logo = true`, and enables BaseLibCompatibilityHelper (required by BaseLib),
     BaseLib, and Packmaster. Never touches ~/.local/share/SlayTheSpire2/steam/.
  2. Build mod:
     - Run `dotnet build` by default (copies .dll/.pdb/.json in ~1s).
     - Run `dotnet publish` (Godot headless `.pck` export) only when `Packmaster.pck` is missing,
       `--pack` is passed, or any asset under `Packmaster/Packmaster/`, `project.godot`, or
       `export_presets.cfg` is newer than `Packmaster.pck`.
  3. Launch `SlayTheSpire2 --headless --force-steam=off --audio-driver Dummy`
     with `PACKMASTER_AUTOTEST=1` (and a hard `timeout 90` backstop).
  4. In-engine runner executes tests sequentially on the Godot main thread, writes
     `[AUTOTEST]` lines via `Console.WriteLine` + `Console.Out.Flush()`, and exits via
     `NGame.Instance.GetTree().Quit(failed == 0 ? 0 : 1)`.
  5. `scripts/test.sh` requires exit code == 0 AND the `[AUTOTEST] SUMMARY: ALL PASSED` sentinel.
```

### How the In-Engine Runner Hooks In (Main Thread, Zero Harmony Patches)
- All test files live under `Packmaster/Testing/` (sibling of `Config/`, NOT inside `Packmaster/Packmaster/`), as plain C# classes (never `Node`/`GodotObject`, and **never** `[HarmonyPatch]`).
- In `MainFile.Initialize()`, if `Environment.GetEnvironmentVariable("PACKMASTER_AUTOTEST") == "1"`, schedule a single Godot main-thread deferred callback:
  `Callable.From(PackmasterAutoTestRunner.Start).CallDeferred();`
- `Start()` launches `_ = RunAllAsync();` on the Godot main thread (`GodotSynchronizationContext`, never `ConfigureAwait(false)`):
  1. `await NGame.Instance.GameStartupComplete;`
  2. Enable `NonInteractiveMode.AutoSlayerCheck = () => true;` and `SaveManager.Instance.PrefsSave.FastMode = FastModeType.Instant;` so animations and UI waits never stall headless execution.
  3. Hook `TaskScheduler.UnobservedTaskException` and `AppDomain.CurrentDomain.UnhandledException` into an error counter so background/unobserved exceptions fail the test suite.
  4. Execute an explicit `(string Name, Func<Task> Run)[]` list (deterministic order, filtered by optional `PACKMASTER_AUTOTEST_FILTER` substring), installing a fresh `CardSelectorStack.SetTestSelector(new AutoSlayCardSelector(new Rng(1)))` per test.
  5. Between run/combat tests, tear down cleanly in `finally` (clean up `RunManager`).
  6. Always flush stdout (`Console.Out.Flush()`) and call `NGame.Instance.GetTree().Quit(exitCode)` in `finally`.

## Test Categories (Growing Incrementally With Features)
1. **Static / Registry / Asset / SmartFormat Integrity (always first, <1s):**
   - `ThePackmaster`, `PackmasterCardPool`, `PackmasterRelicPool`, `PackmasterPotionPool` exist in `ModelDb`.
   - Every Packmaster card, relic, power, potion, and character has existing Godot resources (`ResourceLoader.Exists(...)` for portraits, icons, scenes).
   - Every localization key exists **and** renders cleanly via `GetFormattedText()` (verifying `SmartFormat` variables like `{Damage:diff()}` and `{Block:diff()}` match `CanonicalVars` with zero exceptions or unresolved tokens).
   - Every registered pack has valid metadata and all referenced cards belong to `PackmasterCardPool`.
2. **Pack System & Card Pool Filtering Tests (added with Phase 2):**
   - Starting a run assigns/drafts active packs deterministically per seed.
   - `PackmasterCardPool.GetUnlockedCards(...)` only returns Basic + active-pack cards during a run, returns all cards when `AllPacksMode` is enabled or outside a run, and persists across save/load.
3. **Real Singleplayer Run & Combat Execution Tests:**
   - Start a singleplayer run via `NGame.Instance.StartNewSingleplayerRun(Player.CreateForNewRun<ThePackmaster>(...), shouldSave: false, ...)` with a pinned seed.
   - Verify starting HP, starting deck, and starting relic(s).
   - Enter a combat encounter (`RunManager.Instance.EnterRoomDebug` or `FightConsoleCmd`), rely on the per-test `CardSelectorStack.SetTestSelector(new AutoSlayCardSelector(...))` for selection prompts, enqueue real card plays (`TryManualPlay` / `PlayCardAction`), and assert exact state changes (damage dealt, Block gained, powers applied, energy/draw/exhaust/discard changes) with zero unhandled exceptions.

## Simplicity Guardrails
- No external test framework NuGet downloads — a ~25-line `AutoTestAssert` helper + explicit `(string, Func<Task>)[]` list is sufficient.
- No mouse-clicking UI automation unless testing a bespoke screen; use native game APIs (`StartNewSingleplayerRun`, `TestCardSelector`, `CombatManager`) for speed and determinism.
