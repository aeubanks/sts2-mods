using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Powers;

/// <summary>
/// Empower (StrengthOverTurnsPower): At the start of your next Amount turn(s), gain Strength.
/// Tracks an exact per-turn gain queue so overlapping Empower / Cultist stacks resolve accurately.
/// </summary>
public sealed class StrengthOverTurnsPower : CollectorPower
{
    private List<int> _turnGains = [];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("StrengthPerTurn", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public static async Task<StrengthOverTurnsPower?> ApplyAsync(
        PlayerChoiceContext choiceContext,
        Creature target,
        int turns,
        int strengthPerTurn,
        CardModel? cardSource)
    {
        if (turns <= 0 || strengthPerTurn <= 0 || !target.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return null;

        var existing = target.GetPower<StrengthOverTurnsPower>();
        if (existing is null)
        {
            var created = await PowerCmd.Apply<StrengthOverTurnsPower>(choiceContext, target, turns, target, cardSource);
            created?.AddSchedule(turns, strengthPerTurn);
            return created;
        }

        int delta = Math.Max(0, turns - existing.Amount);
        if (delta > 0)
        {
            await PowerCmd.Apply<StrengthOverTurnsPower>(choiceContext, target, delta, target, cardSource);
        }
        else
        {
            existing.Flash();
        }
        existing.AddSchedule(turns, strengthPerTurn);
        return existing;
    }

    private void AddSchedule(int turns, int strengthPerTurn)
    {
        AssertMutable();
        while (_turnGains.Count < turns)
            _turnGains.Add(0);
        for (int i = 0; i < turns; i++)
            _turnGains[i] += strengthPerTurn;

        RefreshDisplayVar();
    }

    private void RefreshDisplayVar()
    {
        DynamicVars["StrengthPerTurn"].BaseValue = _turnGains.Count > 0 ? _turnGains[0] : 1;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _turnGains = [.. _turnGains];
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int gain;
        if (_turnGains.Count > 0)
        {
            gain = _turnGains[0];
            _turnGains.RemoveAt(0);
        }
        else
        {
            gain = Math.Max(1, DynamicVars["StrengthPerTurn"].IntValue);
        }

        RefreshDisplayVar();
        Flash();
        if (gain > 0)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, gain, Owner, null);
        }

        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Decrement(this);
        }
    }
}
