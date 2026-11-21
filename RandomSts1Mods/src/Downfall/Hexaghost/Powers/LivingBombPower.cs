using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Living Bomb: The next time this creature's Soulburn detonates, it affects ALL enemies.
/// Consumed on detonation.
/// </summary>
public sealed class LivingBombPower : HexaPower, ISoulburnAoeModifier
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HexaCard.SoulburnHoverTip
    ];

    public async Task OnSoulburnAoeDetonatedAsync(PlayerChoiceContext choiceContext, Creature primaryTarget)
    {
        if (primaryTarget == Owner
            && Owner.IsAlive
            && Owner.Powers.Contains(this)
            && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Remove(this);
        }
    }
}
