using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Guardian.Cards;

[Pool(typeof(TokenCardPool))]
public sealed class SentryWave() : GuardianCard(0, CardType.Skill, CardRarity.Token, TargetType.AnyEnemy, showInCardLibrary: false)
{
    public override bool CanBeGeneratedInCombat => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(1m),
        new IntVar("Brace", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [HoverTipFactory.FromPower<WeakPower>(), BraceHoverTip, HoverTipFactory.FromCard<SentryBeam>(true), StasisHoverTip]
            : [HoverTipFactory.FromPower<WeakPower>(), HoverTipFactory.FromCard<SentryBeam>(false), StasisHoverTip];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars.Weak.BaseValue,
            Owner.Creature,
            this);

        if (IsUpgraded && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.BraceAsync(choiceContext, Owner, DynamicVars["Brace"].IntValue);
        }

        if (CombatState is not null
            && Owner.Creature.IsAlive
            && !CombatManager.Instance.IsOverOrEnding
            && GuardianMechanics.CanPlaceInStasis(Owner))
        {
            var beam = CombatState.CreateCard<SentryBeam>(Owner);
            if (IsUpgraded)
                CardCmd.Upgrade(beam);
            await GuardianMechanics.PlaceInStasisAsync(choiceContext, Owner, beam);
        }
    }
}
