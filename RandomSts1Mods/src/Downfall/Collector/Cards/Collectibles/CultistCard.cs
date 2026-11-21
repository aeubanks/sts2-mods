using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Cards.Collectibles;

/// <summary>
/// Cultist (Collectible, 1 -> 0 cost, Power, Pyre):
/// Gain 1 Strength at the start of your next 3 turns.
/// </summary>
public sealed class CultistCard() : CollectibleCard(1, CardType.Power, TargetType.Self)
{
    public override bool IsPyre => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        PyreHoverTip,
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await StrengthOverTurnsPower.ApplyAsync(choiceContext, Owner.Creature, turns: 3, strengthPerTurn: 1, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
