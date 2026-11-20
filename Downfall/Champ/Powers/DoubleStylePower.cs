using MegaCrit.Sts2.Core.Entities.Powers;

namespace Downfall.Champ.Powers;

public sealed class DoubleStylePower : ChampPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}
