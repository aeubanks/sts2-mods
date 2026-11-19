using System.Reflection;
using BaseLib.Config;
using Godot.Bridge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using Packmaster.Config;

namespace Packmaster;

[ModInitializer("Initialize")]
public static class MainFile
{
    public const string ModId = "Packmaster";

    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();
        ScriptManagerBridge.LookupScriptsInAssembly(assembly);

        ModConfigRegistry.Register(ModId, new PackmasterConfig());

        var harmony = new Harmony(ModId);
        harmony.PatchAll(assembly);

        Logger.Info($"{ModId} initialized.");
    }
}
