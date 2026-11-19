using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Hermit.Cards;

public sealed class Scavenge()
    : HermitCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "scavenge";

    public override bool UsesDeadOn => true;
    public override bool CanBeGeneratedInCombat => false;
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<PlatingPower>(4m),
        new GoldVar(5)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ..base.ExtraHoverTips,
        HoverTipFactory.FromPower<PlatingPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(PlatingPower)].BaseValue,
            Owner.Creature,
            this);

        int triggers = await ConsumeDeadOnTriggersAsync(cardPlay);
        if (triggers > 0)
        {
            await PlayerCmd.GainGold(DynamicVars.Gold.IntValue * triggers, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(PlatingPower)].UpgradeValueBy(1m);
        DynamicVars.Gold.UpgradeValueBy(5m);
    }
}
