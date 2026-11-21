using Downfall.Guardian.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Guardian.Cards;

public sealed class RevengeProtocol() : GuardianCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<RevengePower>(2m),
        new PowerVar<BracePerTurnPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DefensiveModeHoverTip,
        HoverTipFactory.FromPower<StrengthPower>(),
        BraceHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal strAmt = DynamicVars[nameof(RevengePower)].BaseValue;
        await PowerCmd.Apply<RevengePower>(choiceContext, Owner.Creature, strAmt, Owner.Creature, this);

        if (Owner.Creature.HasPower<DefensiveModePower>()
            && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, strAmt, Owner.Creature, this);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<BracePerTurnPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars[nameof(BracePerTurnPower)].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(RevengePower)].UpgradeValueBy(1m);
        DynamicVars[nameof(BracePerTurnPower)].UpgradeValueBy(2m);
    }
}
