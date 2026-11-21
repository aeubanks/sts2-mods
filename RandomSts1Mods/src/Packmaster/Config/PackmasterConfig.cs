using BaseLib.Config;

namespace Packmaster.Config;

public class PackmasterConfig : SimpleModConfig
{
    [ConfigHoverTip]
    public static bool AllPacksMode { get; set; } = false;

    [ConfigHoverTip]
    [ConfigSlider(3.0, 10.0, 1.0)]
    [ConfigVisibleIf(nameof(AllPacksMode), Invert = true)]
    public static int PacksPerRun { get; set; } = 7;
}
