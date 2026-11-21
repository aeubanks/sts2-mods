using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Slimebound.Cards;

public sealed class SlimeboundDissolve() : SlimeCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Lick>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);

        if (Owner.PlayerCombatState is not { } pcs
            || CombatState is null
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        if (pcs.Hand.Cards.Count == 0)
            return;

        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        var chosen = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, _ => true, this)).FirstOrDefault();
        if (chosen is null)
            return;

        int lickCount = chosen.EnergyCost.CostsX
            ? pcs.Energy
            : Math.Max(0, chosen.EnergyCost.GetWithModifiers(CostModifiers.All));

        await CardCmd.Exhaust(choiceContext, chosen);

        for (int i = 0; i < lickCount; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;

            var lick = CombatState.CreateCard<Lick>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(lick, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
