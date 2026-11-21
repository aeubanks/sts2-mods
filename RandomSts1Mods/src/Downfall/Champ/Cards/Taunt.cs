using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Champ.Cards;

public sealed class Taunt()
    : ChampCard(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    public override TargetType TargetType =>
        IsUpgraded ? TargetType.AllEnemies : TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(1m),
        new PowerVar<VulnerablePower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var targets = IsUpgraded
            ? CombatState!.HittableEnemies.ToList()
            : [cardPlay.Target ?? throw new ArgumentNullException(nameof(cardPlay.Target))];

        foreach (var target in targets)
        {
            if (!target.IsAlive || CombatManager.Instance.IsOverOrEnding)
                continue;

            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                target,
                DynamicVars[nameof(WeakPower)].BaseValue,
                Owner.Creature,
                this);

            if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await PowerCmd.Apply<VulnerablePower>(
                    choiceContext,
                    target,
                    DynamicVars[nameof(VulnerablePower)].BaseValue,
                    Owner.Creature,
                    this);
            }
        }
    }
}
