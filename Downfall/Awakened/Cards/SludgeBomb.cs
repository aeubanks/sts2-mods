using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Awakened.Cards;

public sealed class SludgeBomb()
    : AwakenedCard(0, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    protected override bool IsPlayable =>
        CombatState is not null
        && Owner?.PlayerCombatState is { } pcs
        && pcs.ExhaustPile.Cards.Any(c => c is StsCards.Void);

    protected override bool ShouldGlowGoldInternal => IsPlayable;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(18m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<StsCards.Void>(),
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        var exhaustedVoid = CardPile.GetCards(Owner, PileType.Exhaust).FirstOrDefault(c => c is StsCards.Void);
        if (exhaustedVoid is not null)
        {
            await CardPileCmd.RemoveFromCombat(exhaustedVoid);
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
