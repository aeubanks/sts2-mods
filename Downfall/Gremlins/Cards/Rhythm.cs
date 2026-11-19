using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Gremlins.Cards;

public sealed class Rhythm()
    : GremlinCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SwapHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await GremlinMob.SwapNextAsync(Owner, CombatState, choiceContext);
        }

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var basics = PileType.Draw.GetPile(Owner).Cards
            .Where(c => c.Rarity == CardRarity.Basic)
            .OrderBy(c => c.Id.Entry, StringComparer.Ordinal)
            .ToList();
        if (basics.Count == 0)
            return;

        CardModel? picked;
        if (basics.Count == 1)
        {
            picked = basics[0];
        }
        else
        {
            var prompt = new LocString("cards", "DOWNFALL-RHYTHM.selectionScreenPrompt");
            var prefs = new CardSelectorPrefs(prompt, 1);
            picked = (await CardSelectCmd.FromSimpleGrid(choiceContext, basics, Owner, prefs)).FirstOrDefault();
        }

        if (picked is null || !Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CardPileCmd.Add(picked, PileType.Hand);
        if (picked.Pile?.Type == PileType.Hand && !picked.EnergyCost.CostsX)
        {
            picked.SetToFreeThisTurn();
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
