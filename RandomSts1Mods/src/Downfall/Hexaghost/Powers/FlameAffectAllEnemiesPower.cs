using MegaCrit.Sts2.Core.Entities.Powers;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Hexaburn (InfernoPotionPower): Ghostflame ignition effects affect ALL enemies
/// and trigger Amount times.
/// </summary>
public sealed class FlameAffectAllEnemiesPower : HexaPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}
