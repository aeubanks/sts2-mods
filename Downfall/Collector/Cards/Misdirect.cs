using Downfall.Collector.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Misdirect (Common, 0 cost, Skill):
/// Gain 2 (4) Block. Next turn, gain 1 Reserve.
/// </summary>
public sealed class Misdirect() : CollectorCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(2m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ReservesHoverTip
    ];

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<NextTurnReservePower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
