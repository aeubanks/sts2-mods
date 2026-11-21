using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Snecko.Cards;

public sealed class Amass() : SneckoCard(2, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    private int _capturedHandCost;
    private bool _hasCapturedHandCost;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(9m, ValueProp.Move)
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _capturedHandCost = 0;
        _hasCapturedHandCost = false;
    }

    private int ComputeHandEnergyCost(bool includeSelf)
    {
        if (Owner?.PlayerCombatState is null)
            return 0;

        int total = 0;
        foreach (var c in PileType.Hand.GetPile(Owner).Cards)
        {
            if (c.EnergyCost.CostsX)
                continue;
            int cost = c.EnergyCost.GetWithModifiers(CostModifiers.All);
            if (cost > 0)
                total += cost;
        }

        if (includeSelf && !EnergyCost.CostsX)
        {
            int selfCost = EnergyCost.GetWithModifiers(CostModifiers.All);
            if (selfCost > 0)
                total += selfCost;
        }

        return total;
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        await base.AfterCardChangedPiles(card, oldPileType, clonedBy);

        if (ReferenceEquals(card, this))
        {
            if (Pile?.Type == PileType.Play && CombatState is not null)
            {
                _capturedHandCost = ComputeHandEnergyCost(includeSelf: oldPileType == PileType.Hand);
                _hasCapturedHandCost = true;
            }
            else if (Pile?.Type != PileType.Play)
            {
                _capturedHandCost = 0;
                _hasCapturedHandCost = false;
            }
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && cardPlay.PlayIndex >= cardPlay.PlayCount - 1)
        {
            _capturedHandCost = 0;
            _hasCapturedHandCost = false;
        }
        return base.AfterCardPlayed(choiceContext, cardPlay);
    }

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (cardSource != this
            || CombatState is null
            || Owner is null
            || target != Owner.Creature
            || !props.IsPoweredCardOrMonsterMoveBlock())
        {
            return 0m;
        }

        if (Pile?.Type == PileType.Play && _hasCapturedHandCost)
            return _capturedHandCost;

        return Pile?.Type == PileType.Hand ? ComputeHandEnergyCost(includeSelf: false) : 0m;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
