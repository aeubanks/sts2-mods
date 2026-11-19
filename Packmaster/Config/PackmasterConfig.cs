using BaseLib.Config;

namespace Packmaster.Config;

public class PackmasterConfig : SimpleModConfig
{
    [ConfigHoverTip]
    public static bool PlaceholderOption { get; set; } = false;
}
