using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Packmaster.Cards.Farmer;

public sealed class SoilResiliency()
    : PackmasterCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const decimal MaxStrength = 10m;

    protected override string PortraitKey => "soil_resiliency";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(1m),
        new IntVar("UsesLeft", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override bool ShouldGlowGoldInternal =>
        CombatState is not null
        && FarmerHelper.GetDistinctCardTypesPlayedThisTurn(Owner, CombatState) >= 3;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.PlayIndex == 0)
        {
            DynamicVars["UsesLeft"].BaseValue = Math.Max(0m, DynamicVars["UsesLeft"].BaseValue - 1m);
        }

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Strength.BaseValue,
            Owner.Creature,
            this);

        if (FarmerHelper.GetDistinctCardTypesPlayedThisTurn(Owner, CombatState, cardPlay) >= 3)
        {
            DynamicVars.Strength.BaseValue = Math.Min(MaxStrength, DynamicVars.Strength.BaseValue * 2m);
        }
    }

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        if (card == this && DynamicVars["UsesLeft"].IntValue <= 1)
        {
            location.pileType = PileType.Exhaust;
        }
        return location;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["UsesLeft"].UpgradeValueBy(1m);
    }
}
