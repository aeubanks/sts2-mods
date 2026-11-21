using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Gremlins.Cards;

public sealed class Tricksy()
    : GremlinCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(4)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SwapHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawn = (await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner)).ToList();

        var toDiscard = drawn
            .Where(c => c.Type != CardType.Attack && c.Pile?.Type == PileType.Hand)
            .ToList();
        if (toDiscard.Count > 0 && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardCmd.Discard(choiceContext, toDiscard);
        }

        if (CombatState is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GremlinMob.SwapToSpecificAsync(Owner, CombatState, GremlinKind.Sneaky, choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(2m);
    }
}
