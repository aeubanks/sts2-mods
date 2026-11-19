using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.Distortion;

public sealed class Deconstruct()
    : PackmasterCard(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies)
{
    protected override string PortraitKey => "deconstruct";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<DistortionPower>(5m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<DistortionPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        await PowerCmd.Apply<DistortionPower>(
            choiceContext,
            CombatState.HittableEnemies,
            DynamicVars[nameof(DistortionPower)].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(DistortionPower)].UpgradeValueBy(1m);
    }
}
