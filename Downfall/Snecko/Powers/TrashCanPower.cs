using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace Downfall.Snecko.Powers;

public sealed class TrashCanPower : SneckoPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    public override async Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var hand = PileType.Hand.GetPile(player).Cards;
        if (hand.Count == 0)
            return;

        Flash();
        var prompt = new LocString("gameplay_ui", "DOWNFALL-TRASH_CAN.selectionScreenPrompt");
        var prefs = new CardSelectorPrefs(prompt, 0, Math.Min(Amount, hand.Count));
        var selected = (await CardSelectCmd.FromHand(choiceContext, player, prefs, _ => true, this)).ToList();
        foreach (var card in selected)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            if (card.Pile?.Type == PileType.Hand)
            {
                await CardCmd.Exhaust(choiceContext, card);
            }
        }
    }
}
