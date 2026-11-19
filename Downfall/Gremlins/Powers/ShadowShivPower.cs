using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Gremlins.Powers;

public sealed class ShadowShivPower : GremlinPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Shiv>()
    ];

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.Card.Type != CardType.Attack
            || cardPlay.ResultPile == PileType.Exhaust
            || cardPlay.Card.Keywords.Contains(CardKeyword.Exhaust)
            || Amount <= 0
            || Owner.Player is not { } player
            || Owner.CombatState is not { } combatState
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            var shiv = combatState.CreateCard<Shiv>(player);
            await CardPileCmd.AddGeneratedCardToCombat(shiv, PileType.Hand, player);
        }
    }
}
