using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Dark Lord Form (Rare, 4 cost, Power):
/// At the start of your turn, play You Are Mine (Upgraded: You Are Mine+) on a random enemy.
/// </summary>
public sealed class DarkLordForm() : CollectorCard(4, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<YouAreMine>(IsUpgraded)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (IsUpgraded)
            await PowerCmd.Apply<DarkLordFormPowerPlus>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        else
            await PowerCmd.Apply<DarkLordFormPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }
}
