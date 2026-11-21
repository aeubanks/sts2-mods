using Downfall.Snecko;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards.Collectibles;

/// <summary>
/// Snecko (Collectible, 0 cost, Skill, Exhaust): Draw 3 (5) cards. Muddle your Hand.
/// </summary>
public sealed class SneckoCard() : CollectibleCard(0, CardType.Skill, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        Downfall.Snecko.SneckoCard.MuddleHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (Owner.PlayerCombatState is not { } pcs || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        foreach (var handCard in pcs.Hand.Cards.ToList())
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            if (SneckoMechanics.CanMuddle(handCard))
                await SneckoMechanics.MuddleAsync(choiceContext, Owner, handCard);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(2m);
    }
}
