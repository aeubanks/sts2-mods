using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Powers;

/// <summary>
/// Empower (StrengthOverTurnsPower): At the start of your next Amount turn(s), gain StrengthPerTurn Strength.
/// </summary>
public sealed class StrengthOverTurnsPower : CollectorPower
{
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

    public void AddStrengthPerTurn(int strength)
    {
        AssertMutable();
        DynamicVars["StrengthPerTurn"].BaseValue = Math.Max(DynamicVars["StrengthPerTurn"].IntValue, strength);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Amount <= 0 || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int str = Math.Max(1, DynamicVars["StrengthPerTurn"].IntValue);
        Flash();
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, str, Owner, null);
        if (Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Decrement(this);
    }
}
