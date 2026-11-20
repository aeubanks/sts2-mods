using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace Downfall.Snecko.Relics;

public sealed class UnknownEgg : SneckoRelic
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SneckoCard.OffclassHoverTip
    ];

    public override Task AfterObtained()
    {
        var upgradable = PileType.Deck.GetPile(Owner).Cards
            .Where(c => SneckoOffclass.IsOffclass(c, Owner) && c.IsUpgradable)
            .ToList()
            .StableShuffle(Owner.PlayerRng.NonCombatGeneration)
            .Take(DynamicVars.Cards.IntValue)
            .ToList();

        if (upgradable.Count > 0)
        {
            Flash();
            foreach (var card in upgradable)
            {
                CardCmd.Upgrade(card);
            }
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyCardRewardOptionsLate(
        Player player,
        List<CardCreationResult> cardRewards,
        CardCreationOptions options)
    {
        if (player != Owner || options.Flags.HasFlag(CardCreationFlags.NoHookUpgrades))
            return false;

        return UpgradeOffclassResults(cardRewards);
    }

    public override void ModifyMerchantCardCreationResults(Player player, List<CardCreationResult> cards)
    {
        if (player == Owner)
        {
            UpgradeOffclassResults(cards);
        }
    }

    public override bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel? newCard)
    {
        newCard = null;
        if (card.Owner != Owner
            || !SneckoOffclass.IsOffclass(card, Owner)
            || !card.IsUpgradable
            || card.CurrentUpgradeLevel >= 1)
        {
            return false;
        }

        newCard = Owner.RunState.CloneCard(card);
        CardCmd.Upgrade(newCard, CardPreviewStyle.None);
        return true;
    }

    private bool UpgradeOffclassResults(List<CardCreationResult> cards)
    {
        bool modified = false;
        foreach (var result in cards)
        {
            var card = result.Card;
            if (SneckoOffclass.IsOffclass(card, Owner)
                && card.IsUpgradable
                && card.CurrentUpgradeLevel < 1)
            {
                var upgraded = Owner.RunState.CloneCard(card);
                CardCmd.Upgrade(upgraded, CardPreviewStyle.None);
                result.ModifyCard(upgraded, this);
                modified = true;
            }
        }
        return modified;
    }
}
