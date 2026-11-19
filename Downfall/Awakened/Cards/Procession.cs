using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using StsCards = MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Awakened.Cards;

public sealed class Procession()
    : AwakenedCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> CardHoverTips =>
    [
        HoverTipFactory.FromCard<StsCards.Void>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null || Owner.PlayerCombatState is not { } pcs)
            return;

        var drawCards = pcs.DrawPile.Cards
            .OrderBy(c => c.Rarity)
            .ThenBy(c => c.Type)
            .ThenBy(c => c.Id.Entry, StringComparer.Ordinal)
            .ToList();
        if (drawCards.Count == 0)
            return;

        var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
        var chosen = (await CardSelectCmd.FromSimpleGrid(choiceContext, drawCards, Owner, prefs)).FirstOrDefault();
        if (chosen is null)
            return;

        int voidCount = chosen.Keywords.Contains(CardKeyword.Unplayable)
            ? 0
            : chosen.EnergyCost.CostsX
                ? pcs.Energy
                : Math.Max(0, chosen.EnergyCost.GetAmountToSpend());

        Creature? target = null;
        bool canAutoPlay = !chosen.Keywords.Contains(CardKeyword.Unplayable);
        if (chosen.TargetType == TargetType.AnyEnemy)
        {
            var enemies = CombatState.HittableEnemies;
            if (enemies.Count > 0)
            {
                target = enemies[Owner.RunState.Rng.CombatTargets.NextInt(enemies.Count)];
            }
            else
            {
                canAutoPlay = false;
            }
        }

        if (canAutoPlay)
        {
            await CardCmd.AutoPlay(choiceContext, chosen, target);
        }

        if (CombatManager.Instance.IsOverOrEnding || !Owner.Creature.IsAlive)
            return;

        for (int i = 0; i < voidCount; i++)
        {
            var voidCard = CombatState.CreateCard<StsCards.Void>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(voidCard, PileType.Draw, Owner, CardPilePosition.Random);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
