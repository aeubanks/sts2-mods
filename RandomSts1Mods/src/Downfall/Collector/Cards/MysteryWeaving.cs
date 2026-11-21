using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Collector.Cards;

/// <summary>
/// Mystery Weaving (Common, 0 cost, Skill):
/// Gain 9 (12) Block. Grants 2 less Block for each other card in your hand.
/// </summary>
public sealed class MysteryWeaving() : CollectorCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(9m, ValueProp.Move),
        new IntVar("PenaltyPerCard", 2m)
    ];

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (cardSource != this
            || CombatState is null
            || Owner?.PlayerCombatState is null
            || target != Owner.Creature
            || !props.IsPoweredCardOrMonsterMoveBlock()
            || Pile?.Type is not (PileType.Hand or PileType.Play))
        {
            return 0m;
        }

        int otherHandCount = PileType.Hand.GetPile(Owner).Cards.Count(c => !ReferenceEquals(c, this));
        return -otherHandCount * DynamicVars["PenaltyPerCard"].BaseValue;
    }

    protected override async Task OnCollectorPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel? pyredCard)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
