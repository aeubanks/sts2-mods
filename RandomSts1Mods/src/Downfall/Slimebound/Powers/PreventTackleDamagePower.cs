using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Powers;

public sealed class PreventTackleDamagePower : SlimePower, ITackleSelfDamagePrevention
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.TackleHoverTip
    ];

    public async Task<bool> TryPreventTackleSelfDamageAsync()
    {
        if (Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return false;

        Flash();
        await PowerCmd.Decrement(this);
        return true;
    }
}
