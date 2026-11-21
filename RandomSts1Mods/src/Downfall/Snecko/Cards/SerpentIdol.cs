using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Snecko.Cards;

public sealed class SerpentIdol() : SneckoCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        OffclassHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        var pool = SneckoOffclass.GetOffclassPool(Owner, inCombat: true);
        if (pool.Count == 0)
            return;

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        int pickCount = Math.Min(DynamicVars.Cards.IntValue, pool.Count);
        var choices = new List<CardModel>(pickCount);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = rng.NextInt(pool.Count);
            var canonical = pool[idx];
            pool.RemoveAt(idx);
            var card = CombatState.CreateCard(canonical, Owner);
            choices.Add(card);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: true);
        if (chosen is null)
            return;

        if (!chosen.EnergyCost.CostsX)
        {
            chosen.SetToFreeThisTurn();
        }
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
