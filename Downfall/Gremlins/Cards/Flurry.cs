using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Cards;

public sealed class Flurry()
    : GremlinCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    private const string CalculatedHitsKey = "CalculatedHits";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new CalculationBaseVar(0m),
        new CalculationExtraVar(1m),
        new CalculatedVar(CalculatedHitsKey).WithMultiplier((card, _) =>
            card.CombatState is { } cs
                ? CombatManager.Instance.History.CardPlaysFinished.Count(e =>
                    e.HappenedThisTurn(cs) && e.CardPlay.Player == card.Owner)
                : 0)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = (int)((CalculatedVar)DynamicVars[CalculatedHitsKey]).Calculate(cardPlay.Target);
        if (hits <= 0)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
