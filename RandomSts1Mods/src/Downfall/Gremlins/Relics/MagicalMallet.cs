using Downfall.Gremlins.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Gremlins.Relics;

public sealed class MagicalMallet : GremlinRelic
{
    private const int MaxTriggersPerTurn = 3;

    private int _remaining;
    private bool _inCombat;

    public override RelicRarity Rarity => RelicRarity.Uncommon;
    public override bool ShowCounter => _inCombat && _remaining > 0;
    public override int DisplayAmount => _remaining;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WizPower>(1m),
        new DynamicVar("MaxPerTurn", MaxTriggersPerTurn)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<WizPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _remaining = 0;
        _inCombat = false;
    }

    private void ResetTurnCounter(bool inCombat)
    {
        _inCombat = inCombat;
        _remaining = inCombat ? MaxTriggersPerTurn : 0;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }

    public override Task AfterObtained()
    {
        if (CombatManager.Instance.IsInProgress)
        {
            ResetTurnCounter(true);
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        ResetTurnCounter(true);
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Creature.Side && participants.Contains(Owner.Creature))
        {
            ResetTurnCounter(true);
        }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetTurnCounter(false);
        return Task.CompletedTask;
    }

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not WeakPower
            || applier != Owner.Creature
            || power.Owner.Side == Owner.Creature.Side
            || amount <= 0m
            || _remaining <= 0
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _remaining--;
        if (_remaining <= 0)
            Status = RelicStatus.Disabled;
        InvokeDisplayAmountChanged();
        Flash();

        await PowerCmd.Apply<WizPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(WizPower)].BaseValue,
            Owner.Creature,
            null);
    }
}
