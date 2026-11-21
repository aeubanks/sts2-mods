using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Powers;

public sealed class MudshieldPower : SneckoPower, ISneckoMuddleListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.MuddleHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public async Task OnCardMuddledAsync(PlayerChoiceContext choiceContext, CardModel card)
    {
        if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
    }
}
