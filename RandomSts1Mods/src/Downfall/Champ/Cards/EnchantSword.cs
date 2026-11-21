using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Champ.Cards;

public sealed class EnchantSword()
    : ChampCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const string BonusKey = "Bonus";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(BonusKey, 8m)
    ];

    private bool IsValidTarget(CardModel c) =>
        !ReferenceEquals(c, this)
        && (c.DynamicVars.ContainsKey("Damage")
            || (c.DynamicVars.ContainsKey("CalculatedDamage") && c.DynamicVars.ContainsKey("CalculationBase")));

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

        if (selected is not null)
        {
            decimal bonus = DynamicVars[BonusKey].BaseValue;
            if (selected.DynamicVars.ContainsKey("Damage"))
            {
                selected.DynamicVars.Damage.BaseValue += bonus;
            }
            else if (selected.DynamicVars.ContainsKey("CalculatedDamage")
                && selected.DynamicVars.ContainsKey("CalculationBase"))
            {
                selected.DynamicVars.CalculationBase.BaseValue += bonus;
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
