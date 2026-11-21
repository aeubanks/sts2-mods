using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Powers;

public sealed class AdaptPower : HermitPower
{
    private const decimal BlockPerExhaust = 8m;

    protected override string IconKey => "adapt_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust),
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || Amount <= 0
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var hand = PileType.Hand.GetPile(player);
        if (hand.Cards.Count == 0)
            return;

        var prompt = new LocString("gameplay_ui", "DOWNFALL-ADAPT.selectionScreenPrompt");
        var prefs = new CardSelectorPrefs(prompt, 0, Math.Min(Amount, hand.Cards.Count));
        var chosen = (await CardSelectCmd.FromHand(choiceContext, player, prefs, null, this)).ToList();
        if (chosen.Count == 0)
            return;

        Flash();
        foreach (var card in chosen)
        {
            await CardCmd.Exhaust(choiceContext, card);
            await CreatureCmd.GainBlock(Owner, BlockPerExhaust, ValueProp.Unpowered, null);
        }
    }
}
