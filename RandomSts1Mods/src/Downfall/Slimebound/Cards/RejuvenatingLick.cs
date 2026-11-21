using Downfall.Slimebound.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Slimebound.Cards;

public sealed class RejuvenatingLick() : SlimeCard(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    public override bool IsLick => true;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SlimedPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GoopHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int goop = DynamicVars["SlimedPower"].IntValue;
        await SlimeMechanics.ApplyGoopAsync(choiceContext, cardPlay.Target, Owner, goop, this);
        if (cardPlay.Target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await SlimeMechanics.ApplyGoopAsync(choiceContext, cardPlay.Target, Owner, goop, this);
        }

        if (IsUpgraded && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.Draw(choiceContext, 1m, Owner);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
