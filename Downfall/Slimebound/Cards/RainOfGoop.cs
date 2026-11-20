using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Slimebound.Cards;

public sealed class RainOfGoop() : SlimeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SlimedPower>(4m),
        new RepeatVar(3)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GoopHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        int times = DynamicVars.Repeat.IntValue;
        int goop = DynamicVars["SlimedPower"].IntValue;
        for (int i = 0; i < times; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;

            var alive = CombatState.HittableEnemies.Where(e => e.IsAlive).ToList();
            if (alive.Count == 0)
                break;

            var target = alive[Owner.RunState.Rng.CombatTargets.NextInt(alive.Count)];
            await SlimeMechanics.ApplyGoopAsync(choiceContext, target, Owner, goop, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Repeat.UpgradeValueBy(1m);
    }
}
