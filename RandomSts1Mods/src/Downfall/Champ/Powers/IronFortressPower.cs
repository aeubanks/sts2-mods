using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Powers;

public sealed class IronFortressPower : ChampPower, IChampStanceChangedListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public async Task OnStanceChangedAsync(
        PlayerChoiceContext choiceContext,
        ChampStanceKind previous,
        ChampStanceKind current)
    {
        if (current != ChampStanceKind.None
            && previous != current
            && Owner.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
        {
            Flash();
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
        }
    }
}
