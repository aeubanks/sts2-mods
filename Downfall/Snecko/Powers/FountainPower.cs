using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Snecko.Powers;

public sealed class FountainPower : SneckoPower, ISneckoOverflowListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.OverflowHoverTip,
        HoverTipFactory.FromPower<VenomPower>()
    ];

    public async Task OnOverflowTriggeredAsync(PlayerChoiceContext choiceContext, SneckoCard source)
    {
        if (!Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.CombatState is not { } combatState
            || Owner.Player is not { } player)
        {
            return;
        }

        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        if (enemies.Count == 0)
            return;

        var target = enemies[player.RunState.Rng.CombatTargets.NextInt(enemies.Count)];
        Flash();
        await PowerCmd.Apply<VenomPower>(choiceContext, target, Amount, Owner, null);
    }
}
