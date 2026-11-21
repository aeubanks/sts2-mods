using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Batter;

public sealed class WideAngle()
    : PackmasterCard(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "wide_angle";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Steal", 7m),
        new PowerVar<WeakPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int stolen = Math.Min(cardPlay.Target.Block, DynamicVars["Steal"].IntValue);
        if (stolen > 0)
        {
            await CreatureCmd.LoseBlock(choiceContext, cardPlay.Target, stolen, Owner.Creature);
            await CreatureCmd.GainBlock(Owner.Creature, stolen, ValueProp.Unpowered, cardPlay);
        }

        if (cardPlay.Target.IsAlive)
        {
            await PowerCmd.Apply<WeakPower>(
                choiceContext,
                cardPlay.Target,
                DynamicVars["WeakPower"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Steal"].UpgradeValueBy(3m);
    }
}
