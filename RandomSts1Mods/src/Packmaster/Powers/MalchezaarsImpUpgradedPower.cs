using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Packmaster.Cards.Warlock;

namespace Packmaster.Powers;

public sealed class MalchezaarsImpUpgradedPower : PackmasterPower
{
    protected override string IconKey => "malchezaars_imp_upgraded_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.FromCard<Imp>()
    ];

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner.Creature != Owner || !Owner.IsAlive || Owner.Player is null || card is Imp)
            return;

        Flash();
        var imps = Enumerable.Range(0, Amount)
            .Select(_ => Owner.CombatState!.CreateCard<Imp>(Owner.Player))
            .ToList();
        await CardPileCmd.AddGeneratedCardsToCombat(imps, PileType.Draw, Owner.Player, CardPilePosition.Random);
    }
}
