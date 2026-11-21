using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Cards.Warlock;

public sealed class RunedMithrilRod()
    : PackmasterCard(-1, CardType.Skill, CardRarity.Rare, TargetType.None)
{
    private bool _pendingTrigger;

    protected override string PortraitKey => "runed_mithril_rod";

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Unplayable,
        CardKeyword.Retain
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(15),
        new IntVar("Drawn", 0m)
    ];

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (_pendingTrigger || Pile?.Type != PileType.Hand || card == this || card.Owner != Owner || !Owner.Creature.IsAlive)
            return;

        DynamicVars["Drawn"].BaseValue += 1m;
        if (DynamicVars["Drawn"].IntValue < DynamicVars.Cards.IntValue)
            return;

        if (fromHandDraw)
        {
            _pendingTrigger = true;
            return;
        }

        await TriggerAsync(choiceContext);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (!_pendingTrigger || player != Owner)
            return;

        _pendingTrigger = false;
        if (Pile?.Type == PileType.Hand && Owner.Creature.IsAlive)
        {
            await TriggerAsync(choiceContext);
        }
    }

    private async Task TriggerAsync(PlayerChoiceContext choiceContext)
    {
        foreach (var handCard in PileType.Hand.GetPile(Owner).Cards)
        {
            if (handCard != this
                && !handCard.EnergyCost.CostsX
                && handCard.EnergyCost.GetWithModifiers(CostModifiers.All) > 0)
            {
                handCard.EnergyCost.AddThisTurn(-1, reduceOnly: true);
            }
        }

        DynamicVars["Drawn"].BaseValue = 0m;
        await CardCmd.Discard(choiceContext, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(-5m);
    }
}
