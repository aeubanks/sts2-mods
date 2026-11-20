using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Powers;

public sealed class PreventSlimeDecayPower : SlimePower, IPreventGoopDecay
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.GoopHoverTip
    ];

    public async Task ConsumePreventionAsync()
    {
        if (Amount <= 0)
            return;

        Flash();
        await PowerCmd.Decrement(this);
    }
}
