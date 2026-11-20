using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Guardian.Cards;

public sealed class VentSteam() : GuardianCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override int MaxSockets => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<VulnerablePower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not null)
        {
            foreach (var enemy in CombatState.HittableEnemies.ToList())
            {
                if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                    return;
                if (enemy.IsAlive)
                {
                    await PowerCmd.Apply<VulnerablePower>(
                        choiceContext,
                        enemy,
                        DynamicVars.Vulnerable.BaseValue,
                        Owner.Creature,
                        this);
                }
            }
        }

        await TriggerSocketedGemsAsync(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
    }
}
