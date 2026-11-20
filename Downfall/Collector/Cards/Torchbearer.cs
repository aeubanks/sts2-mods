using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector.Cards;

/// <summary>
/// Torchbearer (Common, 2 cost, Skill, Exhaust):
/// Gain 10 (14) Temp HP.
/// </summary>
public sealed class Torchbearer() : CollectorCard(2, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("TempHp", 10m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        TempHpHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CollectorMechanics.GainTempHpAsync(choiceContext, Owner.Creature, DynamicVars["TempHp"].BaseValue, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["TempHp"].UpgradeValueBy(4m);
    }
}
