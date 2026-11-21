using Downfall.Awakened.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Awakened.Cards;

public sealed class Nihil()
    : AwakenedCard(2, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override bool UsesChant => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ManaburnPower>(13m)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromPower<ManaburnPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is null)
            return;

        await PowerCmd.Apply<ManaburnPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars[nameof(ManaburnPower)].BaseValue,
            Owner.Creature,
            this);

        if (CombatState is null || CombatManager.Instance.IsOverOrEnding)
            return;

        int chantTriggers = TryTriggerChantCount(cardPlay);
        for (int i = 0; i < chantTriggers; i++)
        {
            foreach (var enemy in CombatState.HittableEnemies.ToList())
            {
                if (CombatManager.Instance.IsOverOrEnding)
                    break;
                if (!enemy.IsAlive)
                    continue;

                if (enemy.GetPower<ManaburnPower>() is { } manaburn)
                {
                    await manaburn.OnEnergyDrainedAsync(choiceContext, 1);
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(ManaburnPower)].UpgradeValueBy(4m);
    }
}
