using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Slimebound.Cards;

public sealed class DouseInSlime() : SlimeCard(3, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SlimedPower>(14m),
        new PowerVar<PreventSlimeDecayPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GoopHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await SlimeMechanics.ApplyGoopAsync(choiceContext, cardPlay.Target, Owner, DynamicVars["SlimedPower"].IntValue, this);
        if (cardPlay.Target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<PreventSlimeDecayPower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars["PreventSlimeDecayPower"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
