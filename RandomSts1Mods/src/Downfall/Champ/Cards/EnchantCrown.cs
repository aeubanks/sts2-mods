using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Champ.Cards;

public sealed class EnchantCrown()
    : ChampCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    private bool IsValidTarget(CardModel c) =>
        !ReferenceEquals(c, this)
        && !c.EnergyCost.CostsX
        && c.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0;

    protected override bool IsPlayable =>
        base.IsPlayable
        && (!IsMutable
            || Owner is null
            || CombatState is null
            || PileType.Hand.GetPile(Owner).Cards.Any(IsValidTarget));

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var candidates = PileType.Hand.GetPile(Owner).Cards.Where(IsValidTarget).ToList();
        if (candidates.Count == 0)
            return;

        var selected = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            candidates,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();

        selected?.EnergyCost.SetThisCombat(0, reduceOnly: true);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
