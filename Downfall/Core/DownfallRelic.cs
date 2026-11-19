using BaseLib.Abstracts;

namespace Downfall.Core;

// Shared relic base across all Downfall characters — intentionally carries NO [Pool(...)] attribute.
public abstract class DownfallRelic : CustomRelicModel
{
    protected abstract string CharacterKey { get; }
    protected abstract string IconKey { get; }

    public override string PackedIconPath => $"res://Downfall/images/{CharacterKey}/relics/{IconKey}.png";
    protected override string PackedIconOutlinePath => $"res://Downfall/images/{CharacterKey}/relics/{IconKey}_outline.png";
    protected override string BigIconPath => $"res://Downfall/images/{CharacterKey}/relics/big/{IconKey}.png";

    internal IEnumerable<string> AllIconPaths => [PackedIconPath, PackedIconOutlinePath, BigIconPath];
}
