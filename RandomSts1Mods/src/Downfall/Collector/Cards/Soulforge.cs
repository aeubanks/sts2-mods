using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Soulforge (Uncommon, 1 cost, Skill, Exhaust):
/// Draw a card from your Collected pile. Gain 1 (2) Upgraded copy/copies of it into your hand.
/// </summary>
public sealed class Soulforge() : CollectorCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Copies", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectedHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var drawn = await CollectorMechanics.DrawFromCollectionAsync(choiceContext, Owner);
        if (drawn is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int copies = DynamicVars["Copies"].IntValue;
        for (int i = 0; i < copies; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var copy = drawn.CreateClone();
            if (copy.IsUpgradable)
                CardCmd.Upgrade(copy);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Copies"].UpgradeValueBy(1m);
    }
}
