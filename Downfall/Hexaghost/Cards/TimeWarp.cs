using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Deal 4 (6) damage. Whenever you Advance or Retract (not end-of-turn auto-Advance),
/// return this card from your Discard Pile to your Hand.
/// </summary>
public sealed class TimeWarp()
    : HexaCard(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy),
      IGhostflameAdvanceListener,
      IGhostflameRetractListener
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AdvanceHoverTip,
        RetractHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    public Task OnGhostflameAdvancedAsync(PlayerChoiceContext choiceContext, Player player, bool isEndTurn)
    {
        if (isEndTurn)
            return Task.CompletedTask;
        return TryReturnToHandAsync(player);
    }

    public Task OnGhostflameRetractedAsync(PlayerChoiceContext choiceContext, Player player)
    {
        return TryReturnToHandAsync(player);
    }

    private async Task TryReturnToHandAsync(Player player)
    {
        if (player != Owner
            || Pile?.Type != PileType.Discard
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (PileType.Hand.GetPile(Owner).Cards.Count < CardPile.MaxCardsInHand)
            await CardPileCmd.Add(this, PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
