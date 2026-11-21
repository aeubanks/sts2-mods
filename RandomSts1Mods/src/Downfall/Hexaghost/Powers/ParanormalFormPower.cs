using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Powers;

/// <summary>
/// Poltergeist (ParanormalFormPower): Whenever you play an Ethereal card,
/// a random enemy loses Amount HP.
/// </summary>
public sealed class ParanormalFormPower : HexaPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || !cardPlay.Card.Keywords.Contains(CardKeyword.Ethereal)
            || !Owner.IsAlive
            || Amount <= 0
            || Owner.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        var target = Owner.Player?.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target is null)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            target,
            Amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            Owner);
    }
}
