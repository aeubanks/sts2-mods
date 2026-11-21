using Downfall.Core;
using Downfall.Gremlins.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Gremlins.Powers;

public sealed class GremlinNobPower : GremlinPower, ITempHpDepletedListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public bool PreventsOverflowDamageOnTempHpDepleted => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GremlinCard.TempHpHoverTip,
        HoverTipFactory.FromCard<Bellow>(),
        HoverTipFactory.FromCard<SkullBash>(),
        HoverTipFactory.FromCard<Rush>()
    ];

    public override bool ShouldDie(Creature creature)
    {
        if (creature != Owner || Owner.Player is not { } player)
            return true;

        return !GremlinMob.ShouldPreventDeath(player);
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner || Owner.Player is not { } player)
            return;

        await GremlinMob.HandleActiveGremlinDeathAsync(player);
    }

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner.Player is { } player && Owner.CombatState is { } combatState)
        {
            await GremlinMob.EnterNobAsync(player, combatState, new ThrowingPlayerChoiceContext());
        }
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner.Player
            || Owner.CombatState is not { } combatState
            || !Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await CardPileCmd.AddGeneratedCardToCombat(combatState.CreateCard<Bellow>(player), PileType.Hand, player);
        await CardPileCmd.AddGeneratedCardToCombat(combatState.CreateCard<SkullBash>(player), PileType.Hand, player);
        await CardPileCmd.AddGeneratedCardToCombat(combatState.CreateCard<Rush>(player), PileType.Hand, player);
    }

    public async Task OnTempHpDepletedAsync()
    {
        Flash();
        await PowerCmd.Remove(this);
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (oldOwner.Player is { } player && oldOwner.CombatState is { } combatState)
        {
            await GremlinMob.RevertNobAsync(player, combatState, new ThrowingPlayerChoiceContext());
        }
    }
}
