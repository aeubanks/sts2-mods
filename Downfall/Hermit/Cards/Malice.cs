using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class Malice()
    : HermitCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "malice";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(16m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        bool exhaustedCurse = false;
        var hand = PileType.Hand.GetPile(Owner);
        if (hand.Cards.Any(c => !ReferenceEquals(c, this)))
        {
            var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
            var chosen = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, c => !ReferenceEquals(c, this), this)).FirstOrDefault();
            if (chosen is not null)
            {
                exhaustedCurse = chosen.Type == CardType.Curse;
                await CardCmd.Exhaust(choiceContext, chosen);
            }
        }

        if (exhaustedCurse)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
        else
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
