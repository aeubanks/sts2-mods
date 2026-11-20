using Downfall.Snecko.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Snecko.Powers;

public abstract class TyphoonPowerBase : SneckoPower, ISneckoOverflowListener
{
    protected abstract bool UpgradeCopy { get; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.OverflowHoverTip,
        HoverTipFactory.FromCard<TyphoonFang>(UpgradeCopy)
    ];

    public async Task OnOverflowTriggeredAsync(PlayerChoiceContext choiceContext, SneckoCard source)
    {
        if (source.IsTyphoonAutoPlay
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.CombatState is not { } combatState
            || Owner.Player is not { } player)
        {
            return;
        }

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count == 0)
                break;

            var target = enemies[player.RunState.Rng.CombatTargets.NextInt(enemies.Count)];
            var copy = (TyphoonFang)combatState.CreateCard<TyphoonFang>(player);
            if (UpgradeCopy)
            {
                CardCmd.Upgrade(copy);
            }
            copy.IsTyphoonAutoPlay = true;
            copy.AddKeyword(CardKeyword.Exhaust);
            await CardCmd.AutoPlay(choiceContext, copy, target);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Remove(this);
        }
    }
}

public sealed class TyphoonPower : TyphoonPowerBase
{
    protected override bool UpgradeCopy => false;
}

public sealed class TyphoonPlusPower : TyphoonPowerBase
{
    protected override bool UpgradeCopy => true;
}
