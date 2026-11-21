using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Cards;

public sealed class Repurpose() : SlimeCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [AbsorbHoverTip, SplitHoverTip, CommandHoverTip]
            : [AbsorbHoverTip, SplitHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!await SlimeMechanics.AbsorbLeadingSlimeAsync(choiceContext, Owner))
            return;

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await SlimeMechanics.SpawnSpecialistSlimeAsync(choiceContext, Owner);
        if (IsUpgraded)
        {
            await SlimeMechanics.CommandAsync(choiceContext, Owner);
        }
    }
}
