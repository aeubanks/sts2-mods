using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Hermit.Powers;

public sealed class FatalDesirePower : HermitPower
{
    protected override string IconKey => "fatal_desire_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<StsCards.Injury>()
    ];

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player || Amount <= 0)
            return count;

        return count + 2m * Amount;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || Amount <= 0
            || CombatState is null
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            var injury = CombatState.CreateCard<StsCards.Injury>(player);
            await CardPileCmd.AddGeneratedCardToCombat(injury, PileType.Hand, player);
        }
    }
}
