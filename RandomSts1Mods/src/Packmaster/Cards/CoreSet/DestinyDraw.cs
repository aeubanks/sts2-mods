using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Packmaster.Character;
using Packmaster.Packs;

namespace Packmaster.Cards.CoreSet;

public sealed class DestinyDraw()
    : PackmasterCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override string PortraitKey => "destiny_draw";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var activeIds = PackRunState.GetActivePackIdsForCurrentRun();
        var eligibleByPack = ModelDb.CardPool<PackmasterCardPool>().AllCards
            .Where(c => c.CanBeGeneratedInCombat && c is not DestinyDraw)
            .Select(c => (Card: c, Pack: PackRegistry.GetForCard(c)))
            .Where(x => x.Pack is not null && (activeIds is null || activeIds.Contains(x.Pack.Id)))
            .GroupBy(x => x.Pack!)
            .Select(g => g.Select(x => x.Card).ToList())
            .ToList();

        if (eligibleByPack.Count == 0)
            return;

        var rng = Owner.RunState.Rng.CombatCardGeneration;
        var chosenCanonicals = new List<CardModel>();

        // Pick 1 random card from up to 3 distinct active packs.
        var packBuckets = eligibleByPack.ToList();
        int distinctPackPicks = Math.Min(3, packBuckets.Count);
        for (int i = 0; i < distinctPackPicks; i++)
        {
            int bucketIdx = rng.NextInt(packBuckets.Count);
            var bucket = packBuckets[bucketIdx];
            packBuckets.RemoveAt(bucketIdx);
            chosenCanonicals.Add(bucket[rng.NextInt(bucket.Count)]);
        }

        // If fewer than 3 packs are active, fill remaining slots with distinct cards from eligible packs.
        if (chosenCanonicals.Count < 3)
        {
            var remaining = eligibleByPack
                .SelectMany(b => b)
                .Where(c => !chosenCanonicals.Contains(c))
                .ToList();

            while (chosenCanonicals.Count < 3 && remaining.Count > 0)
            {
                int idx = rng.NextInt(remaining.Count);
                chosenCanonicals.Add(remaining[idx]);
                remaining.RemoveAt(idx);
            }
        }

        var choices = chosenCanonicals.Select(c => CombatState!.CreateCard(c, Owner)).ToList();
        var picked = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: false);
        if (picked is not null)
        {
            if (!picked.EnergyCost.CostsX && picked.EnergyCost.Canonical >= 0)
                picked.EnergyCost.SetThisTurn(0, reduceOnly: true);

            await CardPileCmd.AddGeneratedCardToCombat(picked, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
