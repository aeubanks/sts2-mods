using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Dragon's Trove (Rare, 0 cost, Skill, Pyre, Exhaust):
/// Draw 2 cards from your Collected pile. Gain 1 (2) Reserve.
/// </summary>
public sealed class DragonsTrove() : CollectorCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override bool IsPyre => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Reserves", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        PyreHoverTip,
        CollectedHoverTip,
        ReservesHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        for (int i = 0; i < 2; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await CollectorMechanics.DrawFromCollectionAsync(choiceContext, Owner);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CollectorMechanics.GainReservesAsync(choiceContext, Owner, DynamicVars["Reserves"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Reserves"].UpgradeValueBy(1m);
    }
}
