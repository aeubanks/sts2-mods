using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace Downfall.Collector.Relics;

/// <summary>
/// Bottled Brain (BottledCollectible, Rare):
/// Upon pickup, Upgrade a card in your Collection. Start combats with it on top of your Collected pile.
/// </summary>
public sealed class BottledCollectible : CollectorRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CollectorCard.CollectedHoverTip
    ];

    protected override bool IsAllowedInternal(IRunState runState) =>
        runState.Players.Any(p => CollectorRunState.GetOrCreate(p).Collection.Count > 0);

    public override async Task AfterObtained()
    {
        if (Owner is null)
            return;

        var runData = CollectorRunState.GetOrCreate(Owner);
        if (runData.Collection.Count == 0)
            return;

        var views = runData.CreateCollectionCardViews(Owner);
        if (views.Count == 0)
            return;

        CardModel? chosen;
        if (views.Count == 1)
        {
            chosen = views[0];
        }
        else
        {
            var prompt = new LocString("relics", "DOWNFALL-BOTTLED_COLLECTIBLE.selectPrompt");
            var prefs = new CardSelectorPrefs(prompt, 1);
            chosen = (await CardSelectCmd.FromSimpleGrid(
                new BlockingPlayerChoiceContext(),
                views,
                Owner,
                prefs)).FirstOrDefault();
        }

        if (chosen is null)
            return;

        int idx = views.FindIndex(c => ReferenceEquals(c, chosen));
        if (idx >= 0)
        {
            runData.UpgradeAndBottleAt(Owner, idx);
            Flash();
        }
    }
}
