using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using Packmaster.Powers;

namespace Packmaster.Cards.BitingCold;

public sealed class Frostburn()
    : PackmasterCard(2, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies)
{
    protected override string PortraitKey => "frostburn";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<FrostbitePower>(18m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<FrostbitePower>(),
        HoverTipFactory.FromCard<Burn>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrostbitePower>(
            choiceContext,
            CombatState!.HittableEnemies,
            DynamicVars["FrostbitePower"].BaseValue,
            Owner.Creature,
            this);

        var burn = CombatState.CreateCard<Burn>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(burn, PileType.Discard, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FrostbitePower"].UpgradeValueBy(4m);
    }
}
