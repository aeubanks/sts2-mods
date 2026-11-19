using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Packmaster.Character;
using Packmaster.Packs;

namespace Packmaster.Relics;

internal static class PackRelicHelper
{
    public static async Task OfferRandomPackCardsToDeckAsync(
        Player owner,
        LocString prompt,
        int minSelect,
        int maxSelect)
    {
        var allPacks = PackRegistry.AllPacks;
        if (allPacks.Count == 0)
            return;

        var pack = allPacks[owner.RunState.Rng.Niche.NextInt(allPacks.Count)];
        var cards = ModelDb.CardPool<PackmasterCardPool>().AllCards
            .Where(c => c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
                && PackRegistry.GetForCard(c) == pack)
            .Select(c => owner.RunState.CreateCard(c, owner))
            .ToList();

        if (cards.Count == 0)
            return;

        int clampedMax = Math.Min(maxSelect, cards.Count);
        int clampedMin = Math.Min(minSelect, clampedMax);
        var chosen = (await CardSelectCmd.FromSimpleGrid(
            new BlockingPlayerChoiceContext(),
            cards,
            owner,
            new CardSelectorPrefs(prompt, clampedMin, clampedMax))).ToList();

        if (chosen.Count == 0)
            return;

        var results = new List<CardPileAddResult>(chosen.Count);
        foreach (var card in chosen)
        {
            results.Add(await CardPileCmd.Add(card, PileType.Deck));
        }

        CardCmd.PreviewCardPileAdd(results);
    }
}

public sealed class PmBoosterPack : PackmasterRelic
{
    protected override string IconKey => "pm_booster_pack";

    public override RelicRarity Rarity => RelicRarity.Common;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        Flash();
        await PackRelicHelper.OfferRandomPackCardsToDeckAsync(
            Owner,
            new LocString("relics", "PACKMASTER-PM_BOOSTER_PACK.selectionScreenPrompt"),
            minSelect: 1,
            maxSelect: 1);
    }
}
