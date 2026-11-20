using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

namespace Downfall.Collector;

/// <summary>
/// Collector Rest Site option ("Release" / Stash Away):
/// Choose a card from your Collection to permanently remove and gain 40 Gold.
/// </summary>
public sealed class StashAwayRestSiteOption(Player owner) : RestSiteOption(owner)
{
    public const int GoldGranted = 40;

    public override string OptionId => "DOWNFALL_RELEASE";

    public override bool IsEnabled =>
        CollectorRunState.GetOrCreate(Owner).Collection.Count > 0;

    public override async Task<bool> OnSelect()
    {
        var state = CollectorRunState.GetOrCreate(Owner);
        var views = state.CreateCollectionCardViews(Owner);
        if (views.Count == 0)
            return false;

        var prompt = new LocString("rest_site_ui", "OPTION_DOWNFALL_RELEASE.selectPrompt");
        var prefs = new CardSelectorPrefs(prompt, 1) { Cancelable = true };
        var chosen = (await CardSelectCmd.FromSimpleGrid(
            new BlockingPlayerChoiceContext(),
            views,
            Owner,
            prefs)).FirstOrDefault();

        if (chosen is null)
            return false;

        int index = views.IndexOf(chosen);
        if (index < 0 || !state.RemoveFromCollectionAt(Owner, index))
            return false;

        await PlayerCmd.GainGold(GoldGranted, Owner);
        return true;
    }
}
