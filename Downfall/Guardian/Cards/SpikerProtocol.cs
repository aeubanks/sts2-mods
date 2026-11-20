using Downfall.Guardian.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Guardian.Cards;

public sealed class SpikerProtocol() : GuardianCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<SpikerProtocolPower>(2m),
        new IntVar("Brace", 6m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DefensiveModeHoverTip,
        HoverTipFactory.FromPower<ThornsPower>(),
        BraceHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool wasInDefensiveMode = Owner.Creature.HasPower<DefensiveModePower>();
        int defensiveBefore = Owner.Creature.GetPowerAmount<DefensiveModePower>();
        decimal thornsAmt = DynamicVars[nameof(SpikerProtocolPower)].BaseValue;

        await PowerCmd.Apply<SpikerProtocolPower>(choiceContext, Owner.Creature, thornsAmt, Owner.Creature, this);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.BraceAsync(choiceContext, Owner, DynamicVars["Brace"].IntValue);
        }

        bool modeShiftTriggered = Owner.Creature.GetPowerAmount<DefensiveModePower>() > defensiveBefore;
        if (wasInDefensiveMode && !modeShiftTriggered && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<ThornsPower>(choiceContext, Owner.Creature, thornsAmt, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(SpikerProtocolPower)].UpgradeValueBy(1m);
        DynamicVars["Brace"].UpgradeValueBy(3m);
    }
}
