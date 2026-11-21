using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Packmaster.Cards.Warlock;

public sealed class SeedsOfDestruction()
    : PackmasterCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "seeds_of_destruction";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("DrawImps", 3m),
        new IntVar("DiscardImps", 4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Imp>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var drawOption = CombatState!.CreateCard<SeedsDrawOption>(Owner);
        var discardOption = CombatState.CreateCard<SeedsDiscardOption>(Owner);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(drawOption);
            CardCmd.Upgrade(discardOption);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            [drawOption, discardOption],
            Owner,
            canSkip: false);

        if (chosen is SeedsDrawOption)
        {
            var imps = Enumerable.Range(0, DynamicVars["DrawImps"].IntValue)
                .Select(_ => CombatState.CreateCard<Imp>(Owner))
                .ToList();
            await CardPileCmd.AddGeneratedCardsToCombat(imps, PileType.Draw, Owner, CardPilePosition.Random);
        }
        else if (chosen is SeedsDiscardOption)
        {
            var imps = Enumerable.Range(0, DynamicVars["DiscardImps"].IntValue)
                .Select(_ => CombatState.CreateCard<Imp>(Owner))
                .ToList();
            await CardPileCmd.AddGeneratedCardsToCombat(imps, PileType.Discard, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["DrawImps"].UpgradeValueBy(1m);
        DynamicVars["DiscardImps"].UpgradeValueBy(1m);
    }
}

public sealed class SeedsDrawOption()
    : PackmasterCard(-1, CardType.Skill, CardRarity.Token, TargetType.None, showInCardLibrary: false)
{
    protected override string PortraitKey => "seeds_of_destruction";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Imps", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Imp>()
    ];

    protected override void OnUpgrade()
    {
        DynamicVars["Imps"].UpgradeValueBy(1m);
    }
}

public sealed class SeedsDiscardOption()
    : PackmasterCard(-1, CardType.Skill, CardRarity.Token, TargetType.None, showInCardLibrary: false)
{
    protected override string PortraitKey => "seeds_of_destruction";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Imps", 4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Imp>()
    ];

    protected override void OnUpgrade()
    {
        DynamicVars["Imps"].UpgradeValueBy(1m);
    }
}
