using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Snecko.Powers;

public sealed class UnendingSupplyPower : SneckoPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DownfallEcho.EchoHoverTip,
        SneckoCard.OffclassHoverTip
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.CombatState is not { } combatState)
        {
            return;
        }

        Flash();
        for (int i = 0; i < Amount; i++)
        {
            var generated = SneckoOffclass.GenerateRandomOffclassCombatCard(combatState, player);
            if (generated is null)
                break;

            var echo = DownfallEcho.MakeEcho(generated);
            if (echo is not null)
            {
                await CardPileCmd.AddGeneratedCardToCombat(echo, PileType.Hand, player);
            }
        }
    }
}
