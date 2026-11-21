using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Packmaster.Powers;

namespace Packmaster.Cards.BitingCold;

public sealed class Refrigerate()
    : PackmasterCard(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "refrigerate";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FrostbitePower>(11m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<FrostbitePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool hadFrostbite = IsUpgraded && cardPlay.Target.HasPower<FrostbitePower>();
        await PowerCmd.Apply<FrostbitePower>(
            choiceContext,
            cardPlay.Target,
            DynamicVars["FrostbitePower"].BaseValue,
            Owner.Creature,
            this);

        if (hadFrostbite && cardPlay.Target.GetPower<FrostbitePower>() is { } frostbite)
        {
            await frostbite.TriggerDamageAsync(choiceContext, reduce: false);
        }
    }
}
