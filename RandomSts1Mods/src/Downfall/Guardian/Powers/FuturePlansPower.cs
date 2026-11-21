using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace Downfall.Guardian.Powers;

public sealed class FuturePlansPower : GuardianPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.StasisHoverTip
    ];

    public override async Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0
            || player.Creature != Owner
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || !GuardianMechanics.CanPlaceInStasis(player))
        {
            return;
        }

        var hand = PileType.Hand.GetPile(player);
        if (hand.Cards.Count == 0 || player.PlayerCombatState is not { } pcs)
            return;

        int openSlots = Math.Max(0, pcs.OrbQueue.Capacity - GuardianMechanics.GetStasisOrbs(player).Count);
        int maxSelectable = Math.Min(Math.Min(Amount, hand.Cards.Count), openSlots);
        if (maxSelectable <= 0)
            return;
        var prompt = new LocString("cards", "DOWNFALL-FUTURE_PLANS.selectionScreenPrompt");
        var prefs = new CardSelectorPrefs(prompt, 0, maxSelectable);
        var selected = (await CardSelectCmd.FromHand(choiceContext, player, prefs, null, this)).ToList();
        if (selected.Count == 0)
            return;

        Flash();
        foreach (var card in selected)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding || !GuardianMechanics.CanPlaceInStasis(player))
                break;

            await GuardianMechanics.PlaceInStasisAsync(choiceContext, player, card);
        }
    }
}
