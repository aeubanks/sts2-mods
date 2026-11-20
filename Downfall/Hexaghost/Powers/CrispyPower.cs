using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Extra Crispy: Whenever Soulburn detonates on an enemy, apply Amount Soulburn to them after detonation.
/// </summary>
public sealed class CrispyPower : HexaPower, ISoulburnDetonatedListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.SoulburnHoverTip
    ];

    public async Task OnSoulburnDetonatedAsync(PlayerChoiceContext choiceContext, Creature target, int damage)
    {
        if (!Owner.IsAlive
            || !target.IsAlive
            || target.Side == Owner.Side
            || Amount <= 0
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await HexaMechanics.ApplySoulburnAsync(choiceContext, target, Amount, Owner);
    }
}
