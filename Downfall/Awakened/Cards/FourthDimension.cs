using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Awakened.Cards;

public sealed class FourthDimension()
    : AwakenedCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(3)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState is not { } pcs || !pcs.Hand.Cards.Any(c => !ReferenceEquals(c, this)))
            return;

        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        var chosen = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            prefs,
            c => !ReferenceEquals(c, this),
            this)).FirstOrDefault();

        if (chosen is null)
            return;

        int copies = DynamicVars.Cards.IntValue;
        var clones = new List<CardModel>(copies);
        for (int i = 0; i < copies; i++)
        {
            clones.Add(chosen.CreateClone());
        }

        await CardCmd.Exhaust(choiceContext, chosen);

        if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive)
            return;

        foreach (var clone in clones)
        {
            await CardPileCmd.AddGeneratedCardToCombat(clone, PileType.Draw, Owner, CardPilePosition.Random);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
