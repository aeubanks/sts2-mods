using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Helpers;

namespace Downfall.Core;

public abstract class DownfallPower : CustomPowerModel
{
    protected abstract string CharacterKey { get; }
    protected virtual string IconKey => StringHelper.Slugify(GetType().Name).ToLowerInvariant();

    public override string CustomPackedIconPath => $"res://Downfall/images/{CharacterKey}/powers/{IconKey}.png";
    public override string CustomBigIconPath => $"res://Downfall/images/{CharacterKey}/powers/big/{IconKey}.png";

    internal IEnumerable<string> AllIconPaths => [CustomPackedIconPath, CustomBigIconPath];
}
