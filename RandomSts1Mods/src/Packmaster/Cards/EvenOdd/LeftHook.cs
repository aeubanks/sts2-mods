using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.EvenOdd;

public sealed class LeftHook()
    : PackmasterCard(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "left_hook";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new IntVar("UsesLeft", 7m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override bool ShouldGlowGoldInternal => EvenOddHelper.IsNextPlayOdd(Owner, CombatState);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (cardPlay.PlayIndex == 0)
        {
            DynamicVars["UsesLeft"].BaseValue = Math.Max(0m, DynamicVars["UsesLeft"].BaseValue - 1m);
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        if (card != this)
            return location;

        if (DynamicVars["UsesLeft"].IntValue <= 1)
        {
            location.pileType = PileType.Exhaust;
        }
        else if (EvenOddHelper.IsNextPlayOdd(Owner, CombatState) && location.pileType == PileType.Discard)
        {
            location.pileType = PileType.Hand;
        }

        return location;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
