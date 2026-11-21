using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Greenpyre Locus (Uncommon, 1 cost, Skill):
/// Choose 1 of 3 Collected cards to add into your hand. Add 2 (3) copies of it into your Collected pile.
/// </summary>
public sealed class GreenpyreLocus() : CollectorCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Copies", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectedHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        var pool = CollectorMechanics.AllPoolCollectibleTypes.ToList();
        int pickCount = Math.Min(3, pool.Count);
        var choices = new List<CardModel>(pickCount);
        for (int i = 0; i < pickCount; i++)
        {
            int idx = rng.NextInt(pool.Count);
            var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(pool[idx]));
            choices.Add(CombatState.CreateCard(canonical, Owner));
            pool.RemoveAt(idx);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: false);
        if (chosen is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var chosenCanonical = ModelDb.GetById<CardModel>(chosen.Id);
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);

        var cData = CollectorCombatState.Get(CombatState).GetPlayer(Owner);
        int copies = DynamicVars["Copies"].IntValue;
        for (int i = 0; i < copies; i++)
        {
            var copy = CombatState.CreateCard(chosenCanonical, Owner);
            int insertPos = rng.NextInt(cData.CombatCollection.Count + 1);
            cData.CombatCollection.Insert(insertPos, copy);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Copies"].UpgradeValueBy(1m);
    }
}
