using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class Showdown()
    : HermitCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "showdown";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(9m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        var strikes = PileType.Hand.GetPile(Owner).Cards
            .Where(c => !ReferenceEquals(c, this) && c.Tags.Contains(CardTag.Strike))
            .ToList();

        foreach (var strike in strikes)
        {
            if (CombatManager.Instance.IsOverOrEnding || CombatState.HittableEnemies.Count == 0)
                break;
            if (strike.Pile?.Type != PileType.Hand)
                continue;

            var target = strike.TargetType == TargetType.AnyEnemy
                ? Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies)
                : null;
            await CardCmd.AutoPlay(choiceContext, strike, target);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
