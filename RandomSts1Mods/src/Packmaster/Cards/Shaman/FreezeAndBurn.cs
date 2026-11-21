using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using Packmaster.Powers;

namespace Packmaster.Cards.Shaman;

public sealed class FreezeAndBurn()
    : PackmasterCard(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
{
    protected override string PortraitKey => "freeze_and_burn";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new PowerVar<StrengthPower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        decimal strengthLoss = DynamicVars.Strength.BaseValue;
        await PowerCmd.Apply<PiercingWailPower>(
            choiceContext,
            CombatState.HittableEnemies,
            strengthLoss,
            Owner.Creature,
            this);

        await PowerCmd.Apply<FreezeAndBurnPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Damage.BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars.Strength.UpgradeValueBy(1m);
    }
}
