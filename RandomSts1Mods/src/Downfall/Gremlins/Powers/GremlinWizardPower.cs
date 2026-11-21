using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Gremlins.Powers;

public sealed class GremlinWizardPower : GremlinActivePower
{
    public override GremlinKind Kind => GremlinKind.Wizard;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WizPower>()
    ];

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        await base.BeforeCardPlayed(cardPlay);
        if (cardPlay.Card.Owner != Owner.Player
            || cardPlay.Card.Type != CardType.Skill
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<WizPower>(new ThrowingPlayerChoiceContext(), Owner, Amount, Owner, null);
    }
}
