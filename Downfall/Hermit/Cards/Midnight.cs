using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class Midnight()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "midnight";

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<ImpendingDoom>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        var doom = CombatState.CreateCard<ImpendingDoom>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(doom, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
