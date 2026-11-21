using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Collector.Cards;

/// <summary>
/// Fuel the Fire (Basic, 1 cost, Pyre):
/// Next turn, gain 2 Reserves (Upgraded: and draw 1 card).
/// </summary>
public sealed class FuelTheFire() : CollectorCard(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    public override bool IsPyre => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<NextTurnReservePower>(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        PyreHoverTip,
        ReservesHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<NextTurnReservePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(NextTurnReservePower)].BaseValue,
            Owner.Creature,
            this);

        if (IsUpgraded && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }
}
