using Downfall.Slimebound.Orbs;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Cards;

public sealed class Split() : SlimeCard(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SplitHoverTip,
        CommandHoverTip,
        HoverTipFactory.FromOrb<PoisonSlime>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SlimeMechanics.SplitSlimeAsync<PoisonSlime>(choiceContext, Owner);
        await SlimeMechanics.CommandAsync(choiceContext, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
