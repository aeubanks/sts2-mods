using BaseLib.Abstracts;
using BaseLib.Utils;
using Packmaster.Character;

namespace Packmaster.Relics;

[Pool(typeof(PackmasterRelicPool))]
public abstract class PackmasterRelic : CustomRelicModel
{
    protected abstract string IconKey { get; }

    public override string PackedIconPath => $"res://Packmaster/images/relics/{IconKey}.png";
    protected override string PackedIconOutlinePath => $"res://Packmaster/images/relics/{IconKey}_outline.png";
    protected override string BigIconPath => $"res://Packmaster/images/relics/big/{IconKey}.png";

    internal IEnumerable<string> AllIconPaths => [PackedIconPath, PackedIconOutlinePath, BigIconPath];
}
