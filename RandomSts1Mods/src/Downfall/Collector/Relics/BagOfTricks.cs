using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Collector.Relics;

/// <summary>
/// Bag of Tricks (Common):
/// At the start of each combat, draw 2 Collected cards.
/// </summary>
public sealed class BagOfTricks : CollectorRelic
{
    private const int ExtraCards = 2;

    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.CollectedHoverTip
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

        Flash();
        for (int i = 0; i < ExtraCards; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await CollectorMechanics.DrawFromCollectionAsync(choiceContext, player);
        }
    }
}
