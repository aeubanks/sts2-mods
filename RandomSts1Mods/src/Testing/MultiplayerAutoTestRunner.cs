using Downfall.Automaton;
using Downfall.Automaton.Cards;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Connection;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.Timeline;
using MegaCrit.Sts2.Core.Timeline.Epochs;
using MegaCrit.Sts2.Core.Unlocks;
using Packmaster.Character;
using Packmaster.Config;
using Packmaster.Packs;
using Packmaster.UI;

namespace RandomSts1Mods.Testing;

/// <summary>
/// Real 2-process headless ENet multiplayer test runner (Host + Client over 127.0.0.1 UDP).
/// Triggered via MP_AUTOTEST=host|client, MP_PORT=<ushort>, MP_SCENARIO=dual_packmaster|mutator_and_architect.
/// </summary>
internal static class MultiplayerAutoTestRunner
{
    private static int _unhandledErrors;
    private static int _stateDivergences;
    private static string _role = "unknown";

    public static void Start(string role)
    {
        _role = role;
        _ = RunAsync(role);
    }

    private static void Log(string msg)
    {
        Console.WriteLine($"[MP-TEST:{_role}] {msg}");
        Console.Out.Flush();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Assertion failed: {message}");
    }

    private static async Task NextFrame()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static async Task WaitUntil(Func<bool> predicate, string description, double timeoutSeconds = 30.0)
    {
        ulong deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000.0);
        while (!predicate())
        {
            if (Time.GetTicksMsec() > deadline)
                throw new TimeoutException($"Timed out after {timeoutSeconds:0.#}s waiting for: {description}");
            await NextFrame();
        }
    }

    private sealed class HeadlessLobbyListener : IStartRunLobbyListener
    {
        public TaskCompletionSource<(string Seed, List<ActModel> Acts, IReadOnlyList<ModifierModel> Modifiers)> BeginRunTcs { get; } = new();
        public TaskCompletionSource<NetErrorInfo> DisconnectedTcs { get; } = new();

        public void PlayerConnected(StartRunLobbyPlayer player) => Log($"Lobby: player {player.id} connected ({player.character?.Id})");
        public void PlayerChanged(StartRunLobbyPlayer player) => Log($"Lobby: player {player.id} changed (char={player.character?.Id}, ready={player.isReady})");
        public void AscensionChanged() { }
        public void SeedChanged() { }
        public void ModifiersChanged() { }
        public void GameModeChanged() { }
        public void MaxAscensionChanged() { }
        public void RemotePlayerDisconnected(StartRunLobbyPlayer player) => Log($"Lobby: remote player {player.id} disconnected");
        public void BeginRun(string seed, List<ActModel> acts, IReadOnlyList<ModifierModel> modifiers) =>
            BeginRunTcs.TrySetResult((seed, acts, modifiers));
        public void LocalPlayerDisconnected(NetErrorInfo info) => DisconnectedTcs.TrySetResult(info);
    }

    private static async Task RunAsync(string role)
    {
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
            bool isHost = string.Equals(role, "host", StringComparison.OrdinalIgnoreCase);
            ushort port = ushort.TryParse(System.Environment.GetEnvironmentVariable("MP_PORT"), out var p) ? p : (ushort)33771;
            string scenario = System.Environment.GetEnvironmentVariable("MP_SCENARIO") ?? "dual_packmaster";

            Log($"Waiting for NGame.GameStartupComplete (scenario={scenario}, port={port})...");
            await NGame.Instance!.GameStartupComplete.WaitAsync(TimeSpan.FromSeconds(45));
            MegaCrit.Sts2.Core.Logging.Logger.logLevelTypeMap[MegaCrit.Sts2.Core.Logging.LogType.GameSync] = MegaCrit.Sts2.Core.Logging.LogLevel.Debug;
            MegaCrit.Sts2.Core.Logging.Logger.logLevelTypeMap[MegaCrit.Sts2.Core.Logging.LogType.Actions] = MegaCrit.Sts2.Core.Logging.LogLevel.Debug;
            SaveManager.Instance.PrefsSave.FastMode = FastModeType.Instant;
            SaveManager.Instance.Progress.EnableFtues = false;
            SaveManager.Instance.Progress.ObtainEpochOverride(EpochModel.GetId<NeowEpoch>(), EpochState.Revealed);
            PackmasterConfig.AllPacksMode = false;
            PackmasterConfig.PacksPerRun = 7;

            CharacterModel hostChar = scenario == "dual_packmaster"
                ? ModelDb.Character<ThePackmaster>()
                : ModelDb.Character<TheAutomaton>();
            CharacterModel clientChar = ModelDb.Character<ThePackmaster>();
            string seed = scenario == "dual_packmaster" ? "MP_DUAL_PM_01" : "MP_MUT_ARCH_01";

            var runState = await ConnectAndStartRunAsync(isHost, port, isHost ? hostChar : clientChar, seed);

            RunManager.Instance.ChecksumTracker.StateDiverged += (peerId, _) =>
            {
                Interlocked.Increment(ref _stateDivergences);
                Log($"STATE DIVERGENCE detected from peer {peerId}!");
            };

            // Drive REAL NOverlayStack card selection screens so CardSelectCmd executes SyncLocalChoice over ENet!
            using var overlayCts = new CancellationTokenSource();
            var overlayPumpTask = AutoCompleteOverlaySelectionsAsync(overlayCts.Token);

            try
            {
                switch (scenario)
                {
                    case "dual_packmaster":
                        await RunDualPackmasterScenarioAsync(runState, isHost);
                        break;
                    case "mutator_and_architect":
                        await RunMutatorAndArchitectScenarioAsync(runState, isHost);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown MP_SCENARIO: {scenario}");
                }
            }
            finally
            {
                overlayCts.Cancel();
                try { await overlayPumpTask; } catch { }
            }

            // Brief settle so any in-flight checksum packets finish arriving.
            for (int i = 0; i < 15; i++)
                await NextFrame();

            Assert(_stateDivergences == 0, $"Expected 0 state divergences, got {_stateDivergences}");
            Assert(_unhandledErrors == 0, $"Expected 0 unhandled errors, got {_unhandledErrors}");

            Log($"PASS {scenario}");
            exitCode = 0;
        }
        catch (Exception ex)
        {
            Log($"FAIL: {ex}");
            exitCode = 1;
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
            try { RunManager.Instance.CleanUp(graceful: true); } catch { }
            await NextFrame();
            ((SceneTree)Engine.GetMainLoop()).Quit(exitCode);
        }
    }

    private static async Task AutoCompleteOverlaySelectionsAsync(CancellationToken ct)
    {
        var gridCardsField = AccessTools.Field(typeof(NCardGridSelectionScreen), "_cards");
        var gridTcsField = AccessTools.Field(typeof(NCardGridSelectionScreen), "_completionSource");
        var chooseCardsField = AccessTools.Field(typeof(NChooseACardSelectionScreen), "_cards");
        var chooseTcsField = AccessTools.Field(typeof(NChooseACardSelectionScreen), "_completionSource");

        while (!ct.IsCancellationRequested)
        {
            await NextFrame();
            var stack = NOverlayStack.Instance;
            if (stack is null || stack.ScreenCount == 0)
                continue;

            var top = stack.Peek();
            if (top is NCardGridSelectionScreen gridScreen)
            {
                var cards = gridCardsField?.GetValue(gridScreen) as IReadOnlyList<CardModel>;
                var tcs = gridTcsField?.GetValue(gridScreen) as TaskCompletionSource<IEnumerable<CardModel>>;
                if (cards is { Count: > 0 } && tcs is not null && !tcs.Task.IsCompleted)
                {
                    // Brief pause (~3 frames) so remote peer processes BeforeActionPausedForPlayerChoice & NCardFlyPowerVfx runs!
                    for (int i = 0; i < 6; i++)
                        await NextFrame();
                    if (!tcs.Task.IsCompleted && GodotObject.IsInstanceValid(gridScreen))
                    {
                        Log($"Auto-selecting card {cards[0].Id.Entry} on {gridScreen.GetType().Name} (triggers real ENet SyncLocalChoice)");
                        tcs.TrySetResult([cards[0]]);
                        stack.Remove(gridScreen);
                    }
                }
            }
            else if (top is NChooseACardSelectionScreen chooseScreen)
            {
                var cards = chooseCardsField?.GetValue(chooseScreen) as IReadOnlyList<CardModel>;
                var tcs = chooseTcsField?.GetValue(chooseScreen) as TaskCompletionSource<IEnumerable<CardModel>>;
                if (cards is { Count: > 0 } && tcs is not null && !tcs.Task.IsCompleted)
                {
                    for (int i = 0; i < 4; i++)
                        await NextFrame();
                    if (!tcs.Task.IsCompleted && GodotObject.IsInstanceValid(chooseScreen))
                    {
                        Log($"Auto-selecting card {cards[0].Id.Entry} on NChooseACardSelectionScreen (triggers real ENet SyncLocalChoice)");
                        tcs.TrySetResult([cards[0]]);
                        stack.Remove(chooseScreen);
                    }
                }
            }
        }
    }

    private static async Task<RunState> ConnectAndStartRunAsync(bool isHost, ushort port, CharacterModel localChar, string seed)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var listener = new HeadlessLobbyListener();
        StartRunLobby lobby;
        INetGameService netService;

        if (isHost)
        {
            var hostService = new NetHostGameService(PeerVersionInfo.LocalDefault());
            var err = hostService.StartENetHost(port, maxClients: 2);
            Assert(!err.HasValue, $"StartENetHost failed on port {port}: {err}");
            netService = hostService;

            lobby = new StartRunLobby(GameMode.Standard, isCustom: false, hostService, listener, maxPlayers: 2);
            lobby.AddLocalHostPlayer(new UnlockState(SaveManager.Instance.Progress), 0, 0);
            lobby.SetLocalCharacter(localChar);
            lobby.SetSeed(seed);
            Log($"ENet host listening on 127.0.0.1:{port} as {localChar.Id}");
        }
        else
        {
            JoinResult joinResult = default;
            JoinFlow? joinFlow = null;
            Exception? lastErr = null;
            ulong deadline = Time.GetTicksMsec() + 25000;

            while (Time.GetTicksMsec() < deadline)
            {
                var clientService = new NetClientGameService(PeerVersionInfo.LocalDefault());
                var initializer = new ENetClientConnectionInitializer(2uL, "127.0.0.1", port);
                joinFlow = new JoinFlow(clientService, initializer);
                try
                {
                    joinResult = await joinFlow.Begin(tree);
                    lastErr = null;
                    break;
                }
                catch (Exception ex)
                {
                    lastErr = ex;
                    clientService.Disconnect(NetError.Quit);
                    await Task.Delay(300);
                }
            }

            if (lastErr != null || joinFlow == null || !joinResult.joinResponse.HasValue)
                throw new InvalidOperationException($"Client failed to join ENet host on port {port}: {lastErr}");

            netService = joinFlow.NetService;
            lobby = new StartRunLobby(GameMode.Standard, isCustom: false, netService, listener, -1);
            lobby.InitializeFromMessage(joinResult.joinResponse.Value);
            lobby.SetLocalCharacter(localChar);
            lobby.SetReady(true);
            Log($"ENet client connected to 127.0.0.1:{port} as {localChar.Id}");
        }

        // Pump NetService.Update() every frame until lobby begins the run.
        bool hostReadied = !isHost;
        ulong lobbyDeadline = Time.GetTicksMsec() + 30000;
        while (!listener.BeginRunTcs.Task.IsCompleted)
        {
            netService.Update();

            if (listener.DisconnectedTcs.Task.IsCompleted)
                throw new InvalidOperationException($"Disconnected in lobby: {listener.DisconnectedTcs.Task.Result}");

            if (Time.GetTicksMsec() > lobbyDeadline)
                throw new TimeoutException($"Timed out waiting for lobby BeginRun (players={lobby.Players.Count})");

            if (isHost && !hostReadied && lobby.Players.Count == 2 && lobby.Players.Any(p => p.id == 2uL && p.isReady))
            {
                lobby.SetReady(true);
                hostReadied = true;
                Log("Host readied up -> starting multiplayer run");
            }

            await NextFrame();
        }

        var (runSeed, acts, modifiers) = await listener.BeginRunTcs.Task;
        var players = lobby.GeneratePlayers(runSeed);
        var runState = await NGame.Instance!.StartNewMultiplayerRun(
            players,
            lobby,
            shouldSave: false,
            acts,
            modifiers,
            runSeed,
            lobby.Ascension);
        lobby.CleanUp(disconnectSession: false);

        Log($"Multiplayer run started! LocalNetId={LocalContext.NetId}, Players={string.Join(",", runState.Players.Select(p => $"{p.NetId}:{p.Character.Id.Entry}"))}");
        return runState;
    }

    private static async Task CompleteLocalNeowAsync(RunState runState, bool pickLastDraftOption)
    {
        await WaitUntil(
            () => runState.CurrentRoom is EventRoom er && er.LocalMutableEvent is Neow n && n.CurrentOptions.Count > 0,
            "Neow event options ready");

        var neow = (Neow)((EventRoom)runState.CurrentRoom!).LocalMutableEvent;

        // If local player is Packmaster, complete all Packmaster draft picks first!
        int draftSteps = 0;
        while (neow.CurrentOptions.Any(o => o.TextKey.StartsWith("PACKMASTER_DRAFT_", StringComparison.Ordinal)))
        {
            int idx = pickLastDraftOption ? neow.CurrentOptions.Count - 1 : 0;
            Log($"Choosing Packmaster draft option {idx}: {neow.CurrentOptions[idx].TextKey}");
            RunManager.Instance.EventSynchronizer.ChooseLocalOption(idx);
            draftSteps++;

            int prevSteps = draftSteps;
            await WaitUntil(
                () => neow.CurrentOptions.Count > 0
                    && (!neow.CurrentOptions[0].TextKey.StartsWith($"PACKMASTER_DRAFT_{prevSteps}_", StringComparison.Ordinal)),
                $"Neow draft step {prevSteps} advance");
        }

        // Now pick a vanilla Neow blessing (option 0).
        if (!neow.IsFinished && neow.CurrentOptions.Count > 0 && !neow.CurrentOptions[0].IsProceed)
        {
            Log($"Choosing vanilla Neow option 0: {neow.CurrentOptions[0].TextKey}");
            RunManager.Instance.EventSynchronizer.ChooseLocalOption(0);
            await WaitUntil(
                () => neow.IsFinished || neow.CurrentOptions.Any(o => o.IsProceed),
                "Vanilla Neow blessing completion");
        }
    }

    private static async Task RunDualPackmasterScenarioAsync(RunState runState, bool isHost)
    {
        var pHost = runState.Players.First(p => p.NetId == 1uL);
        var pClient = runState.Players.First(p => p.NetId == 2uL);
        var localPlayer = LocalContext.GetMe(runState)!;

        // Verify TopBar button mounted on BOTH host and client Packmasters.
        var topBar = NRun.Instance?.GlobalUi?.TopBar;
        Assert(topBar is not null, "TopBar present");
        var packsBtn = topBar!.RightContainer?.GetNodeOrNull<Control>(PackmasterTopBarPatch.ButtonNodeName);
        Assert(packsBtn is not null, "PackmasterActivePacksButton mounted in TopBar");

        // Host picks option 0 on every draft step; Client picks last option on every draft step.
        await CompleteLocalNeowAsync(runState, pickLastDraftOption: !isHost);

        // Wait until BOTH players' persisted packs are synced on this process!
        await WaitUntil(
            () => PackRunState.HasPersistedActivePacks(pHost) && PackRunState.HasPersistedActivePacks(pClient),
            "Both Packmasters' drafted packs persisted across peers");

        var hostPacks = PackRunState.GetActivePackIds(pHost)!;
        var clientPacks = PackRunState.GetActivePackIds(pClient)!;

        Log($"Host(1) packs ({hostPacks.Count}): {string.Join(",", hostPacks.OrderBy(x => x))}");
        Log($"Client(2) packs ({clientPacks.Count}): {string.Join(",", clientPacks.OrderBy(x => x))}");

        Assert(hostPacks.Count == PackmasterConfig.PacksPerRun, $"Host pack count expected {PackmasterConfig.PacksPerRun}, got {hostPacks.Count}");
        Assert(clientPacks.Count == PackmasterConfig.PacksPerRun, $"Client pack count expected {PackmasterConfig.PacksPerRun}, got {clientPacks.Count}");
        Assert(!hostPacks.SetEquals(clientPacks), "Host and Client Packmasters must have distinct drafted pack sets");

        // Verify per-player card reward pools strictly match each player's own pack set!
        var hostPoolCards = CardCreationOptions.ForRoom(pHost, RoomType.Monster).GetPossibleCards(pHost).ToList();
        var clientPoolCards = CardCreationOptions.ForRoom(pClient, RoomType.Monster).GetPossibleCards(pClient).ToList();

        Assert(hostPoolCards.All(c => PackRegistry.GetForCard(c) is not { } pk || hostPacks.Contains(pk.Id)),
            "Host reward pool only contains Host's active packs");
        Assert(clientPoolCards.All(c => PackRegistry.GetForCard(c) is not { } pk || clientPacks.Contains(pk.Id)),
            "Client reward pool only contains Client's active packs");

        // Verify Active Packs screen opens & closes cleanly on this peer!
        var screen = PackmasterPacksViewScreen.ShowScreen(localPlayer);
        Assert(screen is not null, "PackmasterPacksViewScreen opened");
        await NextFrame();
        Assert(PackmasterPacksViewScreen.IsOpen, "PackmasterPacksViewScreen active");
        NCapstoneContainer.Instance?.Close();
        await NextFrame();
        Assert(!PackmasterPacksViewScreen.IsOpen, "PackmasterPacksViewScreen closed cleanly");
    }

    private static async Task RunMutatorAndArchitectScenarioAsync(RunState runState, bool isHost)
    {
        var pHost = runState.Players.First(p => p.NetId == 1uL);
        var pClient = runState.Players.First(p => p.NetId == 2uL);

        // 1. Both complete Neow (Host = Automaton vanilla blessing, Client = Packmaster draft + blessing).
        await CompleteLocalNeowAsync(runState, pickLastDraftOption: false);
        await WaitUntil(
            () => PackRunState.HasPersistedActivePacks(pClient),
            "Client Packmaster finished Neow draft");

        // Wait until local Neow event is completely finished (click Proceed if shown) before voting on map.
        var neow = (Neow)((EventRoom)runState.CurrentRoom!).LocalMutableEvent;
        if (!neow.IsFinished && neow.CurrentOptions.Any(o => o.IsProceed))
        {
            int proceedIdx = neow.CurrentOptions.ToList().FindIndex(o => o.IsProceed);
            Log($"Choosing Neow Proceed option {proceedIdx}");
            RunManager.Instance.EventSynchronizer.ChooseLocalOption(proceedIdx);
            await WaitUntil(() => neow.IsFinished, "Local Neow event finished");
        }
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        for (int i = 0; i < 5; i++)
            await NextFrame();

        // 2. Both vote for the same first combat node via real MapSelectionSynchronizer!
        var firstPoint = runState.Map.StartingMapPoint.Children.First();
        Log($"Voting for map coord {firstPoint.coord} ({firstPoint.PointType})");
        RunManager.Instance.MapSelectionSynchronizer.OnLocalPlayerVotedFor(firstPoint.coord);

        await WaitUntil(
            () => runState.CurrentRoom is CombatRoom
                && CombatManager.Instance.IsInProgress
                && !CombatManager.Instance.PlayerActionsDisabled
                && pHost.PlayerCombatState is { Hand.Cards.Count: >= 5 }
                && pClient.PlayerCombatState is { Hand.Cards.Count: >= 5 }
                && RunManager.Instance.ActionExecutor.CurrentlyRunningAction == null,
            "Both peers entered real multiplayer CombatRoom play phase with opening hands drawn",
            timeoutSeconds: 30.0);

        for (int i = 0; i < 15; i++)
            await NextFrame();

        var combatState = ((CombatRoom)runState.CurrentRoom!).CombatState;
        Log($"In multiplayer combat! Round={combatState.RoundNumber}, HostHand={pHost.PlayerCombatState!.Hand.Cards.Count}, ClientHand={pClient.PlayerCombatState!.Hand.Cards.Count}");

        // 3. Deterministically add Mutator + 2 Dazed to Host's hand on BOTH peers (identical NetCombatCard indices),
        // then Host plays Mutator over real ActionQueueSynchronizer + PlayerChoiceSynchronizer!
        var mutator = combatState.CreateCard<Mutator>(pHost);
        var dazed1 = combatState.CreateCard<Dazed>(pHost);
        var dazed2 = combatState.CreateCard<Dazed>(pHost);
        await CardPileCmd.Add(mutator, PileType.Hand, CardPilePosition.Bottom, null, skipVisuals: false);
        await CardPileCmd.Add(dazed1, PileType.Hand, CardPilePosition.Bottom, null, skipVisuals: false);
        await CardPileCmd.Add(dazed2, PileType.Hand, CardPilePosition.Bottom, null, skipVisuals: false);
        pHost.PlayerCombatState.GainEnergy(3);

        // Let both peers settle the deterministic setup before enqueuing the real networked PlayCardAction.
        for (int i = 0; i < 10; i++)
            await NextFrame();

        if (isHost)
        {
            Log("Host enqueuing networked PlayCardAction for CARD.DOWNFALL-MUTATOR (triggers remote player choice!)...");
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new PlayCardAction(mutator, null));
        }

        // Wait until Mutator's effect has resolved on BOTH Host and Client!
        await WaitUntil(
            () => pHost.Creature.GetPowerAmount<StrengthPower>() >= 2
                && pHost.PlayerCombatState.ExhaustPile.Cards.Any(c => c is Dazed)
                && pHost.PlayerCombatState.Hand.Cards.Any(c => c is Mutator),
            "Mutator resolved on both Host and Client over ENet",
            timeoutSeconds: 25.0);

        Log("Mutator remote Power-card player choice resolved cleanly with zero reparent errors!");

        // Let ActionExecutor finish any remaining post-action checksums.
        await RunManager.Instance.ActionExecutor.FinishedExecutingActions();
        for (int i = 0; i < 10; i++)
            await NextFrame();

        // 4. Enter EVENT.THE_ARCHITECT on both peers and complete the victory flow!
        Log("Entering EVENT.THE_ARCHITECT on both peers...");
        await RunManager.Instance.EnterRoomDebug(
            RoomType.Event,
            MapPointType.Ancient,
            ModelDb.Event<TheArchitect>(),
            showTransition: false);

        await WaitUntil(
            () => runState.CurrentRoom is EventRoom er && er.LocalMutableEvent is TheArchitect arch && arch.CurrentOptions.Count > 0,
            "TheArchitect event loaded with options",
            timeoutSeconds: 20.0);

        var architect = (TheArchitect)((EventRoom)runState.CurrentRoom!).LocalMutableEvent;
        Log($"TheArchitect loaded for {LocalContext.GetMe(runState)!.Character.Id.Entry}, initial option: {architect.CurrentOptions[0].TextKey}");

        // Advance through all dialogue lines + click Proceed (WinRun) via real EventSynchronizer!
        for (int step = 0; step < 10 && !RunManager.Instance.HasRunEnded; step++)
        {
            if (architect.CurrentOptions.Count == 0)
            {
                await NextFrame();
                continue;
            }

            string key = architect.CurrentOptions[0].TextKey;
            Log($"TheArchitect choosing option 0: {key}");
            RunManager.Instance.EventSynchronizer.ChooseLocalOption(0);

            if (key == "PROCEED")
                break;

            await WaitUntil(
                () => RunManager.Instance.HasRunEnded
                    || (architect.CurrentOptions.Count > 0 && architect.CurrentOptions[0].TextKey != key),
                $"TheArchitect advance past {key}",
                timeoutSeconds: 15.0);
        }

        await WaitUntil(
            () => RunManager.Instance.HasRunEnded,
            "RunManager.Instance.HasRunEnded (TheArchitect WinRun victory!)",
            timeoutSeconds: 25.0);

        Log("TheArchitect WinRun completed cleanly on multiplayer peer!");
    }
}
