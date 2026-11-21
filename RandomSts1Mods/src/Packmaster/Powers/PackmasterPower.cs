using BaseLib.Abstracts;

namespace Packmaster.Powers;

public abstract class PackmasterPower : CustomPowerModel
{
    protected abstract string IconKey { get; }

    public override string CustomPackedIconPath => $"res://Packmaster/images/powers/{IconKey}.png";
    public override string CustomBigIconPath => $"res://Packmaster/images/powers/big/{IconKey}.png";

    internal IEnumerable<string> AllIconPaths => [CustomPackedIconPath, CustomBigIconPath];
}
