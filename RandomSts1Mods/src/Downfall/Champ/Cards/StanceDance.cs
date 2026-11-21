using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Champ.Cards;

public sealed class StanceDance()
    : ChampCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BerserkerHoverTip,
        DefensiveHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ChampStanceKind chosen = await ChampStances.ChooseStanceAsync(Owner, CombatState!, choiceContext);
        if (chosen != ChampStanceKind.None)
        {
            await ChampStances.TriggerSkillBonusAsync(Owner, choiceContext, chosen, IsUpgraded ? 2 : 1);
        }
    }
}
