using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using Packmaster.Powers;

namespace Packmaster.Cards.Shaman;

public sealed class FlameBlast()
    : PackmasterCard(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "flame_blast";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<IgnitePower>(4m),
        new PowerVar<VulnerablePower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<IgnitePower>(),
        HoverTipFactory.FromPower<VulnerablePower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool hadVulnerable = cardPlay.Target.HasPower<VulnerablePower>();
        decimal ignite = DynamicVars[nameof(IgnitePower)].BaseValue;

        await PowerCmd.Apply<IgnitePower>(choiceContext, cardPlay.Target, ignite, Owner.Creature, this);
        if (hadVulnerable && cardPlay.Target.IsAlive)
        {
            await PowerCmd.Apply<IgnitePower>(choiceContext, cardPlay.Target, ignite, Owner.Creature, this);
        }

        if (cardPlay.Target.IsAlive)
        {
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars.Vulnerable.BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Vulnerable.UpgradeValueBy(1m);
    }
}
