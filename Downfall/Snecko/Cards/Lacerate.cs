using Downfall.Snecko.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Snecko.Cards;

public sealed class Lacerate() : SneckoCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<VenomPower>(4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<VenomPower>(),
        HoverTipFactory.FromCard<Haze>(IsUpgraded)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        await PowerCmd.Apply<VenomPower>(choiceContext, CombatState.HittableEnemies, DynamicVars["VenomPower"].BaseValue, Owner.Creature, this);

        var haze = CombatState.CreateCard<Haze>(Owner);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(haze);
        }
        await CardPileCmd.AddGeneratedCardToCombat(haze, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        // Upgrade grants Haze+ instead of Haze (matching StS 1 Lacerate -> Crippling Cloud+).
    }
}
