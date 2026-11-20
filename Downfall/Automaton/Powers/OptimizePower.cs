using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Optimized (OptimizePower): Upgrade the next Amount Upgradable card(s) you Encode.
/// </summary>
public sealed class OptimizePower : BronzePower, IOnBeforeEncodeListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.EncodeHoverTip
    ];

    public async Task OnBeforeEncodeAsync(PlayerChoiceContext choiceContext, Player player, BronzeCard card)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !card.IsUpgradable
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        CardCmd.Upgrade(card);
        Flash();
        await PowerCmd.Decrement(this);
    }
}
