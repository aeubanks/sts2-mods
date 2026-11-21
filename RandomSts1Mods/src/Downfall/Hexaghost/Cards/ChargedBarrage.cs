using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Apply 6 (8) Soulburn. Repeat for each Ignited Ghostflame.
/// </summary>
public sealed class ChargedBarrage() : HexaCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Soulburn", 6m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SoulburnHoverTip,
        IgniteHoverTip,
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int amount = DynamicVars["Soulburn"].IntValue;
        int totalHits = 1 + HexaMechanics.GetIgnitedCount(Owner);

        for (int i = 0; i < totalHits; i++)
        {
            if (!cardPlay.Target.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await ApplySoulburnAsync(choiceContext, cardPlay.Target, amount);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Soulburn"].UpgradeValueBy(2m);
    }
}
