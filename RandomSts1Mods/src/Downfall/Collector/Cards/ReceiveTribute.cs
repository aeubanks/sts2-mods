using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Receive Tribute (Rare, 0 cost, Skill, Pyre, Exhaust, no combat generation):
/// Choose 1 of 2 (4) Rare cards from other Downfall villains to add into your hand. It gains Retain.
/// </summary>
public sealed class ReceiveTribute() : CollectorCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override bool IsPyre => true;
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("ChoiceCount", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        PyreHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var candidates = ModelDb.AllCardPools
            .Where(p => p.GetType().Namespace?.StartsWith("Downfall.", StringComparison.Ordinal) == true
                        && p != Owner.Character.CardPool)
            .SelectMany(p => p.AllCards)
            .Where(c => c.Rarity == CardRarity.Rare && c.CanBeGeneratedInCombat)
            .Distinct()
            .ToList();

        if (candidates.Count == 0)
            return;

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        int pickCount = Math.Min(DynamicVars["ChoiceCount"].IntValue, candidates.Count);
        var choices = new List<CardModel>(pickCount);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = rng.NextInt(candidates.Count);
            var card = CombatState.CreateCard(candidates[idx], Owner);
            if (!card.Keywords.Contains(CardKeyword.Retain))
                card.AddKeyword(CardKeyword.Retain);
            choices.Add(card);
            candidates.RemoveAt(idx);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: false);
        if (chosen is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ChoiceCount"].UpgradeValueBy(2m);
    }
}
