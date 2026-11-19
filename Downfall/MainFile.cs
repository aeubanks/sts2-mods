using System.Reflection;
using Downfall.Testing;
using Godot;
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace Downfall;

[ModInitializer("Initialize")]
public static class MainFile
{
    public const string ModId = "Downfall";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        var harmony = new Harmony(ModId);
        harmony.PatchAll(assembly);

        if (System.Environment.GetEnvironmentVariable("DOWNFALL_AUTOTEST") == "1")
        {
            Callable.From(DownfallAutoTestRunner.Start).CallDeferred();
        }

        Logger.Info($"{ModId} initialized.");
    }
}
