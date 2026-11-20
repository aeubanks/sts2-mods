using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Powers;

/// <summary>
/// Library (LibraryPower): At the start of your turn, add Amount random card(s) with Encode
/// to your Hand; they cost 0 until played.
/// </summary>
public sealed class LibraryPower : BronzePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        BronzeCard.EncodeHoverTip
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Amount <= 0
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            var card = AutomatonMechanics.CreateRandomEncodableCard(player);
            if (!card.EnergyCost.CostsX)
                card.EnergyCost.SetUntilPlayed(0);
            await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(card, player);
        }
    }
}
