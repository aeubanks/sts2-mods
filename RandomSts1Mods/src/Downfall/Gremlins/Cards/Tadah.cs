using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Gremlins.Cards;

public sealed class Tadah()
    : GremlinCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Ward>(IsUpgraded),
        SwapHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        var ward = CombatState.CreateCard<Ward>(Owner);
        if (IsUpgraded)
            CardCmd.Upgrade(ward);
        await CardPileCmd.AddGeneratedCardToCombat(ward, PileType.Hand, Owner);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GremlinMob.SwapToSpecificAsync(Owner, CombatState, GremlinKind.Wizard, choiceContext);
        }
    }
}
