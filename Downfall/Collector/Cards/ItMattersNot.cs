using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// It Matters Not (Common, 2 cost, Skill):
/// Gain 13 (17) Block. Increase ALL enemy Weak and Vulnerable by 1.
/// </summary>
public sealed class ItMattersNot() : CollectorCard(2, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(13m, ValueProp.Move),
        new IntVar("Increase", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (CombatState is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        decimal inc = DynamicVars["Increase"].BaseValue;
        foreach (var enemy in CombatState.HittableEnemies.Where(e => e.IsAlive).ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            if (!enemy.IsAlive)
                continue;

            if (enemy.HasPower<WeakPower>())
                await PowerCmd.Apply<WeakPower>(choiceContext, enemy, inc, Owner.Creature, this);

            if (enemy.IsAlive && !CombatManager.Instance.IsOverOrEnding && enemy.HasPower<VulnerablePower>())
                await PowerCmd.Apply<VulnerablePower>(choiceContext, enemy, inc, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
