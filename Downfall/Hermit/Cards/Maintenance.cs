using Downfall.Hermit.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Hermit.Cards;

public sealed class Maintenance()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "maintenance";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<MaintenanceStrikePower>(3m),
        new PowerVar<DexterityPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<MaintenanceStrikePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(MaintenanceStrikePower)].BaseValue,
            Owner.Creature,
            this);

        await PowerCmd.Apply<DexterityPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Dexterity.BaseValue,
            Owner.Creature,
            this);

        EnergyCost.AddThisCombat(1);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(MaintenanceStrikePower)].UpgradeValueBy(1m);
        DynamicVars.Dexterity.UpgradeValueBy(1m);
    }
}
