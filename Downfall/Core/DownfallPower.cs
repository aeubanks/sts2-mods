using BaseLib.Abstracts;

namespace Downfall.Core;

public abstract class DownfallPower : CustomPowerModel
{
    protected abstract string CharacterKey { get; }
    protected abstract string IconKey { get; }

    public override string CustomPackedIconPath => $"res://Downfall/images/{CharacterKey}/powers/{IconKey}.png";
    public override string CustomBigIconPath => $"res://Downfall/images/{CharacterKey}/powers/big/{IconKey}.png";

    internal IEnumerable<string> AllIconPaths => [CustomPackedIconPath, CustomBigIconPath];
}
