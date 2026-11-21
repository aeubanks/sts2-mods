using Downfall.Guardian.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Guardian.Cards;

public sealed class Orbwalk() : GuardianCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self), IInStasisCard
{
    public override bool IsVolatile => !IsUpgraded;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsVolatile
            ? [HoverTipFactory.FromPower<StrengthPower>(), VolatileHoverTip]
            : [HoverTipFactory.FromPower<StrengthPower>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Strength.BaseValue,
            Owner.Creature,
            this);
    }

    public async Task OnTurnStartInStasisAsync(PlayerChoiceContext choiceContext, StasisOrb orb)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }
}
