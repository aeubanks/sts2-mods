using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Gremlins.Cards;

public sealed class ArmsTheft()
    : GremlinCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(1m),
        new PowerVar<StrengthPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var target = cardPlay.Target;

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            target,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this);

        if (!target.IsAlive || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        decimal amount = DynamicVars.Strength.BaseValue;
        int targetStrBefore = target.GetPowerAmount<StrengthPower>();
        await PowerCmd.Apply<StrengthPower>(choiceContext, target, -amount, Owner.Creature, this);
        int stolen = Math.Max(0, targetStrBefore - target.GetPowerAmount<StrengthPower>());

        if (stolen > 0 && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, stolen, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Weak.UpgradeValueBy(1m);
        DynamicVars.Strength.UpgradeValueBy(1m);
    }
}
