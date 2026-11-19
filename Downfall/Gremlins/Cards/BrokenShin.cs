using Downfall.Gremlins.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Gremlins.Cards;

public sealed class BrokenShin()
    : GremlinCard(3, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("WeakPerAgony", 4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<AgonyPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int weak = cardPlay.Target.GetPowerAmount<WeakPower>();
        int perAgony = DynamicVars["WeakPerAgony"].IntValue;
        int stacks = perAgony > 0 ? weak / perAgony : 0;
        if (stacks > 0)
        {
            await PowerCmd.Apply<AgonyPower>(choiceContext, cardPlay.Target, stacks, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["WeakPerAgony"].UpgradeValueBy(-1m);
    }
}
