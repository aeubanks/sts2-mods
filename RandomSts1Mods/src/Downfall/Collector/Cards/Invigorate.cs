using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Flash (Invigorate, Common, 1 cost, Skill, Pyre, Exhaust):
/// Choose Trip or Blind (Upgraded: Trip+ or Blind+) to add into your hand.
/// </summary>
public sealed class Invigorate() : CollectorCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool IsPyre => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        PyreHoverTip,
        HoverTipFactory.FromCard<CollectorTrip>(IsUpgraded),
        HoverTipFactory.FromCard<CollectorBlind>(IsUpgraded)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var trip = CombatState.CreateCard<CollectorTrip>(Owner);
        var blind = CombatState.CreateCard<CollectorBlind>(Owner);
        if (IsUpgraded)
        {
            trip.UpgradeInternal();
            blind.UpgradeInternal();
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, [trip, blind], Owner, canSkip: false);
        if (chosen is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
        }
    }
}
