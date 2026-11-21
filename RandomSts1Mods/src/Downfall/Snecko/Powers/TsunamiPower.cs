using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Powers;

public sealed class TsunamiPower : SneckoPower, ISneckoOverflowListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.OverflowHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public async Task OnOverflowTriggeredAsync(PlayerChoiceContext choiceContext, SneckoCard source)
    {
        if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}
