using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Guardian.RestSite;

public sealed class SocketGemRestSiteOption(Player owner) : RestSiteOption(owner)
{
    public override string OptionId => "DOWNFALL_SOCKET_GEM";

    public override bool IsEnabled =>
        GetGems(Owner).Count > 0 && GetSocketableCards(Owner).Count > 0;

    private static List<CardModel> GetGems(Player player) =>
        player.Deck.Cards.Where(c => c is GuardianCard { IsGem: true, ThisGemType: not null }).ToList();

    private static List<CardModel> GetSocketableCards(Player player) =>
        player.Deck.Cards.Where(c => c is GuardianCard { OpenSockets: > 0 }).ToList();

    public override async Task<bool> OnSelect()
    {
        var gems = GetGems(Owner);
        var targets = GetSocketableCards(Owner);
        if (gems.Count == 0 || targets.Count == 0)
            return false;

        var gemPrompt = new LocString("rest_site_ui", "OPTION_DOWNFALL_SOCKET_GEM.selectGemPrompt");
        var gemPrefs = new CardSelectorPrefs(gemPrompt, 1) { Cancelable = true };
        var chosenGem = (await CardSelectCmd.FromSimpleGrid(
            new BlockingPlayerChoiceContext(),
            gems,
            Owner,
            gemPrefs)).FirstOrDefault() as GuardianCard;

        if (chosenGem?.ThisGemType is not { } gemType)
            return false;

        var targetPrompt = new LocString("rest_site_ui", "OPTION_DOWNFALL_SOCKET_GEM.selectTargetPrompt");
        var targetPrefs = new CardSelectorPrefs(targetPrompt, 1) { Cancelable = true };
        var chosenTarget = (await CardSelectCmd.FromSimpleGrid(
            new BlockingPlayerChoiceContext(),
            targets,
            Owner,
            targetPrefs)).FirstOrDefault() as GuardianCard;

        if (chosenTarget is null || !chosenTarget.TrySocketGem(gemType))
            return false;

        await CardPileCmd.RemoveFromDeck(chosenGem);
        return true;
    }
}
