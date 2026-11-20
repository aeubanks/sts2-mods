using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Forgery (Uncommon, 1 cost, Attack):
/// Deal 8 (10) damage. Choose 1 of 2 (3) cards from your Collected pile to add a copy of into your hand.
/// </summary>
public sealed class Forgery() : CollectorCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new IntVar("ChoiceCount", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectedHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is not null)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }

        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var cData = CollectorCombatState.Get(CombatState).GetPlayer(Owner);
        if (cData.CombatCollection.Count == 0)
            return;

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        var pool = cData.CombatCollection.ToList();
        int count = Math.Min(pool.Count, DynamicVars["ChoiceCount"].IntValue);
        var choices = new List<CardModel>(count);
        for (int i = 0; i < count; i++)
        {
            int idx = rng.NextInt(pool.Count);
            choices.Add(pool[idx].CreateClone());
            pool.RemoveAt(idx);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: false);
        if (chosen is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["ChoiceCount"].UpgradeValueBy(1m);
    }
}
