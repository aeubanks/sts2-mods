using Downfall.Awakened.Powers;
using Downfall.Awakened.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Awakened.Cards;

public sealed class Darkleech()
    : AwakenedSpellCard(1, CardType.Skill, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "darkleech";

    private bool HasEyeOfTheOccult =>
        IsMutable && Owner is not null && Owner.Relics.OfType<EyeOfTheOccult>().Any();

    public override TargetType TargetType =>
        HasEyeOfTheOccult ? TargetType.AllEnemies : TargetType.AnyEnemy;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<VulnerablePower>(1m),
        new PowerVar<ManaburnPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<ManaburnPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not null && Owner.Relics.OfType<EyeOfTheOccult>().FirstOrDefault() is { } eye)
        {
            if (CombatState.HittableEnemies.Count > 1)
            {
                eye.TriggerFlash();
            }

            foreach (var enemy in CombatState.HittableEnemies.ToList())
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;
                if (!enemy.IsAlive)
                    continue;

                await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
                await PowerCmd.Apply<ManaburnPower>(choiceContext, enemy, DynamicVars[nameof(ManaburnPower)].BaseValue, Owner.Creature, this);
            }
            return;
        }

        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, cardPlay.Target, DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<ManaburnPower>(choiceContext, cardPlay.Target, DynamicVars[nameof(ManaburnPower)].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
        DynamicVars[nameof(ManaburnPower)].UpgradeValueBy(2m);
    }
}
