using Downfall.Collector.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Collector.Powers;

/// <summary>
/// Dark Lord Form (DarkLordFormPower): At the start of your turn, play You Are Mine on a random enemy.
/// </summary>
public sealed class DarkLordFormPower : CollectorPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<YouAreMine>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatState is null
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var target = CollectorCard.ResolveTarget(player, null);
            if (target is null)
                break;

            var copy = CombatState.CreateCard<YouAreMine>(player);
            await CardCmd.AutoPlay(choiceContext, copy, target);
            if (copy.Pile is not null && !CombatManager.Instance.IsOverOrEnding)
                await CardPileCmd.RemoveFromCombat(copy);
        }
    }
}
