using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Hermit.Relics;

public sealed class ClaspedLocket : HermitRelic
{
    private bool _triggeredThisTurn;

    protected override string IconKey => "clasped_locket";

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2),
        new IntVar("Injuries", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromCard<StsCards.Injury>()
    ];

    public override async Task AfterObtained()
    {
        var oldLocket = Owner.Relics.OfType<OldLocket>().FirstOrDefault();
        if (oldLocket is not null)
        {
            await RelicCmd.Remove(oldLocket);
        }

        int injuries = DynamicVars["Injuries"].IntValue;
        for (int i = 0; i < injuries; i++)
        {
            var injury = Owner.RunState.CreateCard<StsCards.Injury>(Owner);
            await CardPileCmd.Add(injury, PileType.Deck);
        }
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Creature.Side)
        {
            _triggeredThisTurn = false;
            Status = RelicStatus.Active;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisTurn = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override async Task AfterCardDrawn(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool fromHandDraw)
    {
        if (_triggeredThisTurn
            || card.Owner != Owner
            || card.Type != CardType.Curse
            || card.Pile?.Type != PileType.Hand
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisTurn = true;
        Status = RelicStatus.Normal;
        Flash();
        await CardCmd.Exhaust(choiceContext, card);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}
