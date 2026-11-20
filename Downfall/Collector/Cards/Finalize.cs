using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Finalize (Uncommon, 4 cost, Skill, Exhaust, Healing/no combat generation):
/// When the enemy dies, heal 6 (8) HP. Apply 24 (28) Doom.
/// </summary>
public sealed class Finalize() : CollectorCard(4, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<CollectorDoomPower>(24m),
        new PowerVar<HealIfDieThisTurnPower>(6m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DoomHoverTip,
        HoverTipFactory.FromPower<HealIfDieThisTurnPower>()
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        var target = ResolveTarget(Owner, cardPlay.Target);
        if (target is null)
            return;

        await PowerCmd.Apply<HealIfDieThisTurnPower>(
            choiceContext,
            target,
            DynamicVars["HealIfDieThisTurnPower"].BaseValue,
            Owner.Creature,
            this);

        if (target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CollectorMechanics.ApplyDoomAsync(
                choiceContext,
                target,
                DynamicVars["CollectorDoomPower"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["CollectorDoomPower"].UpgradeValueBy(4m);
        DynamicVars["HealIfDieThisTurnPower"].UpgradeValueBy(2m);
    }
}
