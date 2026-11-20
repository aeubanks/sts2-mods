using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace Downfall.Guardian.Relics;

public sealed class SackOfGems : GuardianRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GuardianCard.GemHoverTip
    ];

    public override async Task AfterObtained()
    {
        var choices = GuardianGems.CreateDistinctGemChoicesForDeck(Owner, 5, onlyCommon: false, Owner.RunState.Rng.UpFront);
        if (choices.Count == 0)
            return;

        var prompt = new LocString("relics", "DOWNFALL-SACK_OF_GEMS.selectionScreenPrompt");
        var prefs = new CardSelectorPrefs(prompt, 0, choices.Count);
        var selected = (await CardSelectCmd.FromSimpleGrid(
            new BlockingPlayerChoiceContext(),
            choices,
            Owner,
            prefs)).ToList();

        foreach (var gem in selected)
        {
            await CardPileCmd.Add(gem, PileType.Deck);
        }
    }
}
