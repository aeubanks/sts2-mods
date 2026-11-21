using Downfall.Champ.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Champ.Cards;

public sealed class FancyFootwork()
    : ChampCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FocusedBerPower>(10m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BerserkerHoverTip,
        DefensiveHoverTip,
        FinisherHoverTip,
        HoverTipFactory.FromPower<FocusedBerPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ChampStances.EnterStanceNotInAsync(Owner, choiceContext);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<FocusedBerPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars[nameof(FocusedBerPower)].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(FocusedBerPower)].UpgradeValueBy(5m);
    }
}
