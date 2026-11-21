using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Cards;

public sealed class Chomp() : SlimeCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        TackleHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_bite")
            .Execute(choiceContext);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding || Owner.PlayerCombatState is not { } pcs)
            return;

        var tackles = pcs.Hand.Cards
            .OfType<SlimeCard>()
            .Where(c => c.IsTackle && !c.EnergyCost.CostsX && c.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            .ToList();
        if (tackles.Count == 0)
            return;

        var chosen = tackles[Owner.RunState.Rng.CombatCardGeneration.NextInt(tackles.Count)];
        if (IsUpgraded)
        {
            chosen.EnergyCost.SetThisCombat(0, reduceOnly: true);
        }
        else
        {
            chosen.EnergyCost.SetThisTurn(0, reduceOnly: true);
        }
        NCard.FindOnTable(chosen)?.PlayRandomizeCostAnim();
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
