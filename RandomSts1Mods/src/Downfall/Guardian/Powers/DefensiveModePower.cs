using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Guardian.Powers;

public sealed class DefensiveModePower : GuardianPower
{
    public const int ThornsGrant = 3;

    private int _appliedPlayerRound = -1;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<ThornsPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _appliedPlayerRound = -1;
    }

    public override bool ShouldClearBlock(Creature creature) => creature != Owner;

    public override Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature)
    {
        if (ReferenceEquals(this, preventer))
            Flash();
        return Task.CompletedTask;
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner.CombatState?.CurrentSide == CombatSide.Player)
            _appliedPlayerRound = Owner.CombatState.RoundNumber;
        else
            _appliedPlayerRound = -1;

        GuardianMechanics.SwapGuardianSprite(Owner, defensive: true);

        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<ThornsPower>(new ThrowingPlayerChoiceContext(), Owner, ThornsGrant, Owner, null, silent: true);
        }
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner) || side != Owner.Side || Amount <= 0)
            return;

        if (_appliedPlayerRound == combatState.RoundNumber)
            return;

        await PowerCmd.Decrement(this);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        GuardianMechanics.SwapGuardianSprite(oldOwner, defensive: false);

        if (oldOwner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            var thorns = oldOwner.GetPower<ThornsPower>();
            if (thorns is not null)
            {
                int remove = Math.Min(thorns.Amount, ThornsGrant);
                if (remove > 0)
                {
                    await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), thorns, -remove, oldOwner, null);
                }
            }
        }
    }
}
