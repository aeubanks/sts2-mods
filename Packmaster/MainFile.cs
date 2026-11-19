using System.Reflection;
using BaseLib.Config;
using Godot;
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using Packmaster.Config;
using Packmaster.Packs;
using Packmaster.Testing;

namespace Packmaster;

[ModInitializer("Initialize")]
public static class MainFile
{
    public const string ModId = "Packmaster";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        ModConfigRegistry.Register(ModId, new PackmasterConfig());
        PackDefinitions.RegisterAll();
        PackRunState.Initialize();

        var harmony = new Harmony(ModId);
        harmony.PatchAll(assembly);

        if (System.Environment.GetEnvironmentVariable("PACKMASTER_AUTOTEST") == "1")
        {
            Callable.From(PackmasterAutoTestRunner.Start).CallDeferred();
        }

        Logger.Info($"{ModId} initialized.");
    }
}
