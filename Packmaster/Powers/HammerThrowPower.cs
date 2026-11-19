using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Powers;

public sealed class HammerThrowPower : PackmasterPower
{
    protected override string IconKey => "hammer_throw_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    public override async Task AfterFlush(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyCollection<CardModel> flushedCards,
        IReadOnlyCollection<CardModel> retainedCards)
    {
        if (player != Owner.Player || retainedCards.Count == 0)
            return;

        Flash();
        for (int i = 0; i < retainedCards.Count; i++)
        {
            if (Owner.IsDead)
                break;

            var targets = CombatState.HittableEnemies.ToList();
            if (targets.Count == 0)
                break;

            await CreatureCmd.Damage(choiceContext, targets, Amount, ValueProp.Unpowered, Owner);
        }
    }
}
