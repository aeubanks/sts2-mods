using BaseLib.Cards.Variables;
using BaseLib.Commands;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Packmaster.Cards.Shaman;

public sealed class ReadTheFlames()
    : PackmasterCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override string PortraitKey => "read_the_flames";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3),
        new ScryVar(2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<FadingEmber>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
        await ScryCmd.Execute(choiceContext, this);

        if (CombatState is not null)
        {
            var ember = CombatState.CreateCard<FadingEmber>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(ember, PileType.Draw, Owner, CardPilePosition.Random);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
