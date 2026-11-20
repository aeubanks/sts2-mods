using Downfall.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Apply 8 (12) Soulburn, twice if the enemy already has Soulburn.
/// </summary>
public sealed class BurningTouch() : HexaCard(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Soulburn", 8m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SoulburnHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int amount = DynamicVars["Soulburn"].IntValue;
        bool alreadyBurned = cardPlay.Target.HasPower<SoulburnPower>();

        if (alreadyBurned)
            await ApplySoulburnAsync(choiceContext, cardPlay.Target, amount);

        await ApplySoulburnAsync(choiceContext, cardPlay.Target, amount);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Soulburn"].UpgradeValueBy(4m);
    }
}
