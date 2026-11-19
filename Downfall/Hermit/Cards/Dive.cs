using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class Dive()
    : HermitCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override string PortraitKey => "dive";

    public override bool UsesDeadOn => true;
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move),
        new PowerVar<PlatingPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..base.ExtraHoverTips,
        HoverTipFactory.FromPower<PlatingPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        int triggers = await ConsumeDeadOnTriggersAsync(cardPlay);
        if (triggers > 0)
        {
            await PowerCmd.Apply<PlatingPower>(
                choiceContext,
                Owner.Creature,
                DynamicVars[nameof(PlatingPower)].BaseValue * triggers,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars[nameof(PlatingPower)].UpgradeValueBy(1m);
    }
}
