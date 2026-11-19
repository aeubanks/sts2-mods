using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Gremlins.Cards;

public sealed class Duplicate()
    : GremlinCard(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EchoHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var eligible = PileType.Hand.GetPile(Owner).Cards
            .Where(c => c.Type == CardType.Attack && !GremlinMob.IsEcho(c))
            .ToList();
        if (eligible.Count == 0)
            return;

        CardModel? chosen;
        if (eligible.Count == 1)
        {
            chosen = eligible[0];
        }
        else
        {
            var prompt = new LocString("cards", "DOWNFALL-DUPLICATE.selectionScreenPrompt");
            var prefs = new CardSelectorPrefs(prompt, 1);
            chosen = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner,
                prefs,
                c => c.Type == CardType.Attack && !GremlinMob.IsEcho(c),
                this)).FirstOrDefault();
        }

        if (chosen is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        int count = DynamicVars.Cards.IntValue;
        for (int i = 0; i < count; i++)
        {
            if (GremlinMob.MakeEcho(chosen) is { } echo)
            {
                if (!echo.EnergyCost.CostsX && echo.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0)
                {
                    echo.EnergyCost.AddThisCombat(-1);
                }
                await CardPileCmd.AddGeneratedCardToCombat(echo, PileType.Hand, Owner);
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
