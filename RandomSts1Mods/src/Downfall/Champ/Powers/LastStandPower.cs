using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Champ.Powers;

public sealed class LastStandPower : ChampPower
{
    private bool _triggered;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public static bool IsBelowHalfHp(Creature creature) =>
        creature.CurrentHp * 2 < creature.MaxHp;

    public static async Task TriggerCleanseAndStrengthAsync(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal strengthAmount,
        CardModel? cardSource)
    {
        var debuffs = target.Powers
            .Where(p => p.Type == PowerType.Debuff || (p.Type == PowerType.Buff && p.AllowNegative && p.Amount < 0))
            .ToList();

        foreach (var debuff in debuffs)
        {
            if (!target.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            await PowerCmd.Remove(debuff);
        }

        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, target, strengthAmount, target, cardSource);
        }
    }

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner
            || _triggered
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || !IsBelowHalfHp(Owner))
        {
            return;
        }

        _triggered = true;
        int amount = Amount;
        Flash();
        await PowerCmd.Remove(this);
        await TriggerCleanseAndStrengthAsync(new ThrowingPlayerChoiceContext(), Owner, amount, null);
    }
}
