using Downfall.Gremlins.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Gremlins.Powers;

public sealed class MakingMoreMagicPower : GremlinPower
{
    protected override string IconKey => "making_magic_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Bang>(upgrade: true)
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Amount <= 0
            || Owner.CombatState is not { } combatState
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int count = Amount;
        Flash();
        await PowerCmd.Remove(this);
        for (int i = 0; i < count; i++)
        {
            var bang = combatState.CreateCard<Bang>(player);
            CardCmd.Upgrade(bang);
            await CardPileCmd.AddGeneratedCardToCombat(bang, PileType.Hand, player);
        }
    }
}
