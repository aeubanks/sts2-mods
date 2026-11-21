using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Guardian.Cards;

public sealed class BronzeArmor() : GuardianCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("ReduceAmount", 2m),
        new IntVar("Brace", 8m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BraceHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await GuardianMechanics.ReduceDebuffsAsync(
            choiceContext,
            Owner.Creature,
            DynamicVars["ReduceAmount"].IntValue,
            this);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GuardianMechanics.BraceAsync(choiceContext, Owner, DynamicVars["Brace"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
