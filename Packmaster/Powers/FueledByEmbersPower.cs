using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Packmaster.Cards.Shaman;

namespace Packmaster.Powers;

public sealed class FueledByEmbersPower : PackmasterPower
{
    protected override string IconKey => "fueled_by_embers_power";

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<FadingEmber>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || !Owner.IsAlive
            || Owner.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
            return;

        int count = Amount;
        if (count <= 0)
            return;

        Flash();
        await CardPileCmd.Draw(choiceContext, count, player);

        if (!Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var handEmbers = Enumerable.Range(0, count)
            .Select(_ => combatState.CreateCard<FadingEmber>(player))
            .ToList();
        await CardPileCmd.AddGeneratedCardsToCombat(handEmbers, PileType.Hand, player);

        var drawEmbers = Enumerable.Range(0, count)
            .Select(_ => combatState.CreateCard<FadingEmber>(player))
            .ToList();
        await CardPileCmd.AddGeneratedCardsToCombat(drawEmbers, PileType.Draw, player, CardPilePosition.Random);
    }
}
