using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Cards;

public sealed class MassRepurpose() : SlimeCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [AbsorbHoverTip, SplitHoverTip, CommandHoverTip]
            : [AbsorbHoverTip, SplitHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int absorbed = await SlimeMechanics.AbsorbAllSlimesAsync(choiceContext, Owner);
        for (int i = 0; i < absorbed; i++)
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            await SlimeMechanics.SpawnSpecialistSlimeAsync(choiceContext, Owner);
        }

        if (IsUpgraded && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await SlimeMechanics.CommandAllSlimesAsync(choiceContext, Owner, times: 1);
        }
    }
}
