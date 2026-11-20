using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Guardian.Powers;

public sealed class ModeShiftPower : GuardianPower
{
    public const int StartingThreshold = 20;
    public const int ThresholdIncreasePerActivation = 10;
    public const int MaxThreshold = 50;
    public const int BlockOnTrigger = 16;

    private int _activations;
    private int _pendingReduction;
    private bool _isReducing;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public int Activations => _activations;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.DefensiveModeHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _activations = 0;
        _pendingReduction = 0;
        _isReducing = false;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner
            || result.UnblockedDamage <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        await ReduceAsync(choiceContext, result.UnblockedDamage);
    }

    internal async Task ReduceAsync(PlayerChoiceContext choiceContext, int reduction)
    {
        if (reduction <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Player is not { } player)
        {
            return;
        }

        _pendingReduction += reduction;
        if (_isReducing)
            return;

        _isReducing = true;
        try
        {
            int safety = 0;
            while (_pendingReduction > 0 && safety++ < 10 && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                int apply = _pendingReduction;
                _pendingReduction = 0;

                Flash();

                int triggers = 0;
                int nextAmount = Amount - apply;
                while (nextAmount <= 0 && triggers < 10)
                {
                    triggers++;
                    _activations++;
                    int nextThreshold = Math.Min(StartingThreshold + (_activations * ThresholdIncreasePerActivation), MaxThreshold);
                    nextAmount += nextThreshold;
                }

                nextAmount = Math.Max(1, nextAmount);
                int delta = nextAmount - Amount;
                if (delta != 0)
                {
                    await PowerCmd.ModifyAmount(choiceContext, this, delta, Owner, null);
                }

                for (int i = 0; i < triggers; i++)
                {
                    if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                        break;

                    int turns = Owner.CombatState?.CurrentSide == CombatSide.Player ? 1 : 2;
                    await GuardianMechanics.EnterDefensiveModeAsync(
                        choiceContext,
                        player,
                        turns,
                        baseBlock: BlockOnTrigger,
                        triggerModeShiftListeners: true);
                }
            }
        }
        finally
        {
            _isReducing = false;
            _pendingReduction = 0;
        }
    }
}
