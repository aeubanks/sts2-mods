using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace Downfall.Hermit.Powers;

public sealed class CoalescencePower : HermitPower
{
    protected override string IconKey => "coalescence_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
    ];

    public override async Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player || Amount <= 0 || CombatManager.Instance.IsOverOrEnding)
            return;

        var retainable = PileType.Hand.GetPile(player).Cards.Where(c => !c.ShouldRetainThisTurn).ToList();
        if (retainable.Count > 0)
        {
            Flash();
            var prompt = new LocString("gameplay_ui", "DOWNFALL-COALESCENCE.selectionScreenPrompt");
            var prefs = new CardSelectorPrefs(prompt, 0, Math.Min(Amount, retainable.Count));
            var selected = await CardSelectCmd.FromHand(choiceContext, player, prefs, c => !c.ShouldRetainThisTurn, this);
            foreach (var card in selected)
            {
                card.GiveSingleTurnRetain();
            }
        }

        await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Remove(this);
        }
    }
}
