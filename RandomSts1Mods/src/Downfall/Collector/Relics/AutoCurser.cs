using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Relics;

/// <summary>
/// Hexx Talisman (AutoCurser, Common):
/// At the start of combat, apply 1 Weak and 1 Vulnerable to a random enemy.
/// </summary>
public sealed class AutoCurser : CollectorRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Owner.Creature.CombatState is not { RoundNumber: 1 }
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var target = CollectorCard.ResolveTarget(player, null);
        if (target is null)
            return;

        Flash();
        await PowerCmd.Apply<WeakPower>(choiceContext, target, 1m, Owner.Creature, null);
        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, 1m, Owner.Creature, null);
    }
}
